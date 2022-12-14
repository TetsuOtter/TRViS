from dataclasses import asdict, dataclass
import json
from os import makedirs
from shutil import copyfile
from subprocess import PIPE, Popen
from os.path import dirname, basename, exists, join as joinPath
from sys import argv, stderr
from typing import Dict, List
from xml.etree import ElementTree
from aiofiles import open as aio_open
from aiohttp import ClientSession, TCPConnector
import asyncio
import re
from hashlib import sha256

CSPROJ_PATH = dirname(__file__) + "/../TRViS/TRViS.csproj"
TIMEOUT_SEC = 2
ENC = "utf-8"
IGNORE_NS = "TRViS"

LICENSE_INFO_LIST_FILE_NAME = "license_list.json"

LICENSE_TYPE_EXPRESSION = 'expression'
# `{PackageID}/{FilePath(Name)}`にライセンスファイルが配置されていることを示す
LICENSE_TYPE_FILE = 'file'
# licenseUrlから取得したものについては、ハッシュにてファイルを管理する。
# Type:HASHは、そのハッシュ値が記録されていることを示す。
LICENSE_TYPE_HASH = 'hash'

@dataclass
class PackageInfo:
  PackageName: str
  ResolvedVersion: str

@dataclass
class LicenseInfo:
  id: str
  version: str
  resolvedVersion: str
  license: str
  licenseDataType: str
  licenseUrl: str
  author: str
  projectUrl: str
  copyrightText: str

def getNugetGlobalPackagesDir() -> str:
  with Popen(["dotnet", "nuget", "locals", "global-packages", "-l"], stdout=PIPE) as p:
    execResult = p.stdout.readlines()[0].decode(ENC)
    return execResult.removeprefix("global-packages: ").removesuffix('\n')

async def getLicenseInfo(globalPackagesDir: str, packageInfo: PackageInfo) -> LicenseInfo:
  packageNameLower = str.lower(packageInfo.PackageName)
  resourcePath = f'{globalPackagesDir}{packageNameLower}/{packageInfo.ResolvedVersion}/{packageNameLower}.nuspec'

  metadata: ElementTree.Element = None
  NUSPEC_XML_NAMESPACE = {}
  async with aio_open(resourcePath, 'r') as stream:
    root = ElementTree.fromstring(await stream.read())
    if root is None:
      return None
    # namespaceがパッケージによって違う場合があるため、動的に取得する
    if root.tag.find('{') >= 0:
      NUSPEC_XML_NAMESPACE[''] = root.tag.removeprefix('{').removesuffix('}package')
    metadata = root.find("metadata", NUSPEC_XML_NAMESPACE)

  licenseElem = metadata.find("license", namespaces=NUSPEC_XML_NAMESPACE)
  licenseText: str = None
  licenseDataType: str = None
  if licenseElem is not None:
    licenseText = licenseElem.text
    licenseDataType = licenseElem.attrib['type']

  return LicenseInfo(
    metadata.findtext("id", namespaces=NUSPEC_XML_NAMESPACE),
    metadata.findtext("version", namespaces=NUSPEC_XML_NAMESPACE),
    packageInfo.ResolvedVersion,
    licenseText,
    licenseDataType,
    metadata.findtext("licenseUrl", namespaces=NUSPEC_XML_NAMESPACE),
    metadata.findtext("authors", namespaces=NUSPEC_XML_NAMESPACE)
    or metadata.findtext("owners", namespaces=NUSPEC_XML_NAMESPACE),
    metadata.findtext("projectUrl", namespaces=NUSPEC_XML_NAMESPACE),
    metadata.findtext("copyright", namespaces=NUSPEC_XML_NAMESPACE),
  )

def getAndTrySetUniqueKey(dic: Dict[str, str], key: str) -> str:
  hashStr = sha256(key.encode(ENC)).hexdigest()
  v = dic.get(key)
  if v is not None:
    return v

  # ハッシュ衝突チェック
  # 衝突の可能性は限りなく低いものの、0では無いため
  for v in dic.values():
    if v == hashStr:
      return getAndTrySetUniqueKey(dic, key + hashStr)

  dic[key] = hashStr
  return hashStr

async def getAndWriteFile(session: ClientSession, srcUrl: str, targetPath: str):
  async with aio_open(targetPath, 'w') as f:
    text = ''
    async with session.get(srcUrl) as result:
      if not result.ok:
        raise EOFError(f'GET Request to {srcUrl} failed')

      text = await result.text()

      result.close()
      if not result.closed:
        await result.wait_for_close()
    await f.write(text)

async def dumpLicenseTextFileFromLicenseUrl(session: ClientSession, targetDir: str, licenseInfo: LicenseInfo, urlDic: Dict[str, str]):
  url = licenseInfo.licenseUrl

  hashStr = getAndTrySetUniqueKey(urlDic, url)
  licenseFilePath = joinPath(targetDir, hashStr)
  licenseInfo.license = hashStr
  licenseInfo.licenseDataType = LICENSE_TYPE_HASH
  if exists(licenseFilePath):
    return

  async with session.head(url, allow_redirects=True) as result:
    result.close()
    if not result.closed:
      await result.wait_for_close()

    if not result.ok:
      raise EOFError(f"HEAD request to {url} failed")

    url = result.url.human_repr()

  if url.startswith("https://github.com/"):
    dirs = url.removeprefix("https://github.com/").split('/')
    userName = dirs[0]
    repoName = dirs[1]
    refName = dirs[3]
    path = ""
    for v in dirs[4:]:
      path = '/' + v
    url = f"https://raw.githubusercontent.com/{userName}/{repoName}/{refName}{path}"
  await getAndWriteFile(session, url, licenseFilePath)

async def dumpLicenseTextFileFromLicenseExpression(session: ClientSession, licenseInfo: LicenseInfo, targetDir: str):
  licenseList = [str(v) for v in re.split("\(|\)| ", licenseInfo.license) if (v != '' or v.isspace()) and v != "OR" and v != "AND"]
  for licenseId in licenseList:
    licenseFilePath = joinPath(targetDir, licenseId)
    if exists(licenseFilePath):
      continue

    url = f'https://raw.githubusercontent.com/spdx/license-list-data/master/text/{licenseId}.txt'
    await getAndWriteFile(session, url, licenseFilePath)

def dumpLicenseTextFileFromLicenseFilePath(globalPackagesDir: str, targetDir: str, licenseInfo: LicenseInfo):
  packageNameLower = str.lower(licenseInfo.id)
  resourceDir = f'{globalPackagesDir}{packageNameLower}/{licenseInfo.resolvedVersion}/'
  targetDir = f'{targetDir}/{licenseInfo.id}'
  if not exists(targetDir):
    makedirs(targetDir)
  copyfile(resourceDir + licenseInfo.license, f'{targetDir}/{licenseInfo.license}')
  licenseInfo.license = f'{licenseInfo.id}/{licenseInfo.license}'


async def dumpLicenseTextFile(session: ClientSession, targetDir: str, globalPackagesDir: str, licenseInfo: LicenseInfo, urlDic: Dict[str, str]):
  if licenseInfo.licenseDataType is None:
    await dumpLicenseTextFileFromLicenseUrl(session, targetDir, licenseInfo, urlDic)
  elif licenseInfo.licenseDataType == LICENSE_TYPE_EXPRESSION:
    await dumpLicenseTextFileFromLicenseExpression(session, licenseInfo, targetDir)
  elif licenseInfo.licenseDataType == LICENSE_TYPE_FILE:
    dumpLicenseTextFileFromLicenseFilePath(globalPackagesDir, targetDir, licenseInfo)

def getFrameworkVersion(platform: str) -> str:
  with Popen(["dotnet", "list", CSPROJ_PATH, "package"], stdout=PIPE) as p:
    for line in p.stdout.readlines():
      lineStr = line.decode(ENC)
      frameworkVersionCheckResult = re.search(r"\[net\d+\.\d+-" + platform + r"\d+.\d+\]", lineStr)
      
      if not frameworkVersionCheckResult:
        continue

      return frameworkVersionCheckResult.group().removeprefix('[').removesuffix(']')

async def main(platform: str, targetDir: str) -> int:
  if not exists(targetDir):
    makedirs(targetDir)

  targetFramework = getFrameworkVersion(platform)
  lines: List[List[bytes]]
  with Popen(["dotnet", "list", CSPROJ_PATH, "package", "--framework", targetFramework, '--include-transitive'], stdout=PIPE) as p:
    lines = [line.split() for line in p.stdout.readlines()]
    if len(lines) <= 3:
      return 1
    if p.wait(TIMEOUT_SEC) != 0:
      return p.returncode
  lines = lines[3:]

  packages: List[PackageInfo] = []
  for v in lines:
    if len(v) <= 0 or v[0] != b'>':
      continue
    packages.append(PackageInfo(v[1].decode(ENC), v[-1].decode(ENC)))

  globalPackagesDir = getNugetGlobalPackagesDir()
  packageInfoList = await asyncio.gather(*[getLicenseInfo(globalPackagesDir, v) for v in packages if not v.PackageName.startswith(IGNORE_NS)])

  urlDic = {}
  async with ClientSession(connector = TCPConnector(limit = 2, force_close = True)) as session:
    await asyncio.gather(*[dumpLicenseTextFile(session, targetDir, globalPackagesDir, v, urlDic) for v in packageInfoList])

  with open(f'{targetDir}/{LICENSE_INFO_LIST_FILE_NAME}', 'w') as f:
    json.dump([asdict(v) for v in packageInfoList], f)

  return 0


if __name__ == "__main__":
  if len(argv) <= 2:
    print("too few arguments", file=stderr)
    exit(1)
  exitCode = asyncio.run(main(argv[1], argv[2]))
  exit(exitCode)
