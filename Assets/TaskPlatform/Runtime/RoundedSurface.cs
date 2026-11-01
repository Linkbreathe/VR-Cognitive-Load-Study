using UnityEngine;

namespace EvidenceVR
{
    // Geometry-only rounded panel: no texture download, animation or custom shader.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedSurface : UnityEngine.UI.MaskableGraphic
    {
        public float radius = 24;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            float rad = Mathf.Min(radius, Mathf.Min(r.width,r.height)*.5f);
            vh.AddVert(r.center, color, Vector2.zero);
            const int segments=8;
            for(int corner=0;corner<4;corner++)
            {
                var center = new Vector2(corner==0 || corner==3 ? r.xMax-rad : r.xMin+rad, corner<2 ? r.yMax-rad : r.yMin+rad);
                for(int s=0;s<=segments;s++)
                {
                    float angle=(corner*90f + s*90f/segments)*Mathf.Deg2Rad;
                    vh.AddVert(center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*rad,color,Vector2.zero);
                }
            }
            int count=4*(segments+1);
            for(int i=1;i<=count;i++) vh.AddTriangle(0,i,i==count ? 1 : i+1);
        }
    }
}
