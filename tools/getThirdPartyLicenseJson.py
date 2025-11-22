from dataclasses import asdict, dataclass
import json
from os import makedirs, name as osName
from shutil import copyfile
from subprocess import PIPE, Popen
from os.path import dirname, basename, exists, join as joinPath
from sys import argv, stderr
from typing import Dict, List
from xml.etree import ElementTree
from aiofiles import open as aio_open
from aiohttp import ClientSession, TCPConnector, ClientError
import asyncio
import re
from hashlib import sha256

CSPROJ_PATH = dirname(__file__) + "/../TRViS/TRViS.csproj"
TIMEOUT_SEC = 2
ENC = "utf-8"
DOTNET_ENCODING = 'utf-8-sig' if osName == 'nt' else 'utf-8'

IGNORE_NS = "TRViS"

LICENSE_INFO_LIST_FILE_NAME = "license_list.json"

LICENSE_TYPE_EXPRESSION = 'expression'
# `{PackageID}/{FilePath(Name)}`にライセンスファイルが配置されていることを示す
LICENSE_TYPE_FILE = 'file'
# licenseUrlから取得したものについては、ハッシュにてファイルを管理する。
# Type:HASHは、そのハッシュ値が記録されていることを示す。
LICENSE_TYPE_HASH = 'hash'
LICENSE_TYPE_URL = 'url'

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
  resourcePath = joinPath(globalPackagesDir, packageNameLower, packageInfo.ResolvedVersion, f'{packageNameLower}.nuspec')

  metadata: ElementTree.Element = None
  NUSPEC_XML_NAMESPACE = {}
  async with aio_open(resourcePath, 'r', encoding=DOTNET_ENCODING) as stream:
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
  async with session.get(srcUrl) as result:
    if not result.ok:
      raise EOFError(f'GET Request to {srcUrl} failed (status={result.status})')

    async with aio_open(targetPath, 'wb') as f:
      async for chunk in result.content.iter_chunked(4096):
        await f.write(chunk)

    result.close()
    if not result.closed:
      await result.wait_for_close()

async def dumpLicenseTextFileFromLicenseUrl(session: ClientSession, targetDir: str, licenseInfo: LicenseInfo, urlDic: Dict[str, str]):
  url = licenseInfo.licenseUrl

  if not url:
    # nothing to do when there is no licenseUrl
    return

  hashStr = getAndTrySetUniqueKey(urlDic, url)
  licenseFilePath = joinPath(targetDir, hashStr)
  licenseInfo.license = hashStr
  licenseInfo.licenseDataType = LICENSE_TYPE_HASH
  if exists(licenseFilePath):
    return

  # Try HEAD first to resolve redirects and check availability. If HEAD
  # is not allowed (405, 501, etc.) or fails for any reason, fall back to GET.
  final_url = None
  head_content_type = None
  try:
    async with session.head(url, allow_redirects=True) as result:
      # Keep headers to decide whether we should download the content
      head_content_type = (result.headers.get('content-type') or '').lower()
      result.close()
      if not result.closed:
        await result.wait_for_close()

      # If the server accepts HEAD and it's OK, we have the final URL.
      if result.ok:
        final_url = result.url.human_repr()
      else:
        # Non-OK status from HEAD; fallback to GET below.
        final_url = None
  except ClientError:
    # Head failed (e.g. method not allowed, connection problems). We'll
    # attempt a GET and let that determine the final URL.
    final_url = None
    print(f"HEAD request to {url} failed - falling back to GET", file=stderr)

  get_content_type = None
  get_result = None
  if final_url is None:
    async with session.get(url, allow_redirects=True) as result:
      get_content_type = (result.headers.get('content-type') or '').lower()
      if not result.ok:
        print(f"GET request to {url} failed with status {result.status}; writing placeholder file", file=stderr)
        # create placeholder file so UI can show something instead of failing
        async with aio_open(licenseFilePath, 'w', encoding=ENC) as f:
          await f.write(f"(Cannot download license text from {url} (status={result.status}))")
        licenseInfo.licenseDataType = LICENSE_TYPE_HASH
        licenseInfo.license = hashStr
        return

      final_url = result.url.human_repr()
      # Keep the result content for writing if we determine to download
      # We need to read the content later in getAndWriteFile (which issues another GET),
      # so we don't reuse the 'result' here.

  url = final_url

  # Convert GitHub repository URLs to raw URLs which contain the license text
  if url.startswith("https://github.com/"):
    dirs = url.removeprefix("https://github.com/").split('/')
    userName = dirs[0]
    repoName = dirs[1]
    refName = dirs[3]
    path = ""
    for v in dirs[4:]:
      path = '/' + v
    url = f"https://raw.githubusercontent.com/{userName}/{repoName}/{refName}{path}"
  try:
    # Decide whether to download the license text or treat the URL as an external link
    # Decide whether to treat this URL as a direct license text (downloadable) or an external page
    def content_indicates_download(content_type: str) -> bool:
      if not content_type:
        return False
      c = content_type.lower()
      # text/plain, text/* (not HTML), application/json or xml are considered download
      if c.startswith('text/') and 'html' not in c:
        return True
      if 'json' in c or 'xml' in c:
        return True
      return False

    lower_url = url.lower()
    likely_raw = (
      url.startswith("https://raw.githubusercontent.com/") or
      lower_url.endswith('.txt') or
      lower_url.endswith('.md') or
      'licenses.nuget.org' in lower_url or
      'raw.githubusercontent.com' in lower_url
    )

    # If any of HEAD/GET indicated a downloadable content type or the URL seems raw, download.
    content_type_to_check = get_content_type or head_content_type or ''
    should_download = content_indicates_download(content_type_to_check) or likely_raw

    if not should_download:
      # Treat as URL (external page)
      licenseInfo.licenseDataType = LICENSE_TYPE_URL
      licenseInfo.license = url
      return

    try:
      await getAndWriteFile(session, url, licenseFilePath)
    except EOFError as e:
      print(f"Failed to GET {url}: {e}", file=stderr)
      # Create a placeholder file so the app can display something instead of failing
      async with aio_open(licenseFilePath, 'w', encoding=ENC) as f:
        await f.write(f"(Cannot download license text from {url})")
      # record as a hash-based license file so the UI will load the placeholder
      licenseInfo.licenseDataType = LICENSE_TYPE_HASH
      licenseInfo.license = hashStr
  except EOFError as e:
    print(f"Failed to GET {url}: {e}", file=stderr)
    # Create a placeholder file so the app can display something instead of failing
    async with aio_open(licenseFilePath, 'w', encoding=ENC) as f:
      await f.write(f"(Cannot download license text from {url})")
    # record as a hash-based license file so the UI will load the placeholder
    licenseInfo.licenseDataType = LICENSE_TYPE_HASH
    licenseInfo.license = hashStr

async def dumpLicenseTextFileFromLicenseExpression(session: ClientSession, licenseInfo: LicenseInfo, targetDir: str):
  licenseList = [str(v) for v in re.split(r"\(|\)| ", licenseInfo.license) if (v != '' or v.isspace()) and v != "OR" and v != "AND"]
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
      frameworkVersionCheckResult = re.search(r"\[net\d+\.\d+-" + platform + r"\d+(.\d)+\]", lineStr)

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

  globalPackagesDir = getNugetGlobalPackagesDir().strip()
  packageInfoList = await asyncio.gather(*[getLicenseInfo(globalPackagesDir, v) for v in packages if not v.PackageName.startswith(IGNORE_NS)])

  urlDic = {}
  headers = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
  }
  async with ClientSession(connector = TCPConnector(limit = 2, force_close = True), headers = headers) as session:
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
