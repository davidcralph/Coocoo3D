using Caprice.Display;
namespace RenderPipelines;

public enum LightType
{
    [UIShow(UIShowType.All, "Directional")]
    Directional,
    [UIShow(UIShowType.All, "Point")]
    Point,
}
