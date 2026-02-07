using UnityEngine;
using System.Text;
using System.IO;

public static class RuntimeObjExporter
{
    public static void ExportMesh(Mesh mesh, string filePath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Exported mesh");

        foreach (var v in mesh.vertices)
            sb.AppendLine($"v {v.x} {v.y} {v.z}");

        foreach (var uv in mesh.uv)
            sb.AppendLine($"vt {uv.x} {uv.y}");

        foreach (var n in mesh.normals)
            sb.AppendLine($"vn {n.x} {n.y} {n.z}");

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            var tris = mesh.GetTriangles(sub);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i] + 1;
                int b = tris[i + 1] + 1;
                int c = tris[i + 2] + 1;
                sb.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
            }
        }

        File.WriteAllText(filePath, sb.ToString());
    }


}

//public static class RuntimeTextureExporter
//{
//    public static void ExportTexture(Texture texture, string filePath)
//    {
//        RenderTexture rt = RenderTexture.GetTemporary(
//            texture.width, texture.height, 0, RenderTextureFormat.ARGB32);

//        Graphics.Blit(texture, rt);

//        RenderTexture prev = RenderTexture.active;
//        RenderTexture.active = rt;

//        Texture2D tex = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
//        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
//        tex.Apply();

//        RenderTexture.active = prev;
//        RenderTexture.ReleaseTemporary(rt);

//        File.WriteAllBytes(filePath, tex.EncodeToPNG());
//    }
//}

