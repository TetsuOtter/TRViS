using TRViS.DTAC.Logic.Abstractions;
using TRViS.Services;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that maps DesiredOrientation → AppDisplayOrientation and calls IOrientationService.
/// </summary>
internal class OrientationControllerAdapter : IOrientationController
{
    private readonly IOrientationService _orientationService;

    public OrientationControllerAdapter(IOrientationService orientationService)
    {
        _orientationService = orientationService ?? throw new ArgumentNullException(nameof(orientationService));
    }

    public void SetOrientation(DesiredOrientation orientation)
    {
        AppDisplayOrientation appOrientation = orientation switch
        {
            DesiredOrientation.Portrait => AppDisplayOrientation.Portrait,
            DesiredOrientation.Landscape => AppDisplayOrientation.Landscape,
            _ => AppDisplayOrientation.All,
        };
        _orientationService.SetOrientation(appOrientation);
    }
}
