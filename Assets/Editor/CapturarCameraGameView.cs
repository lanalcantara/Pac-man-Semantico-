using UnityEngine;
using UnityEditor;
using System.IO;

public static class CapturarCameraGameView
{
    [InitializeOnLoadMethod]
    private static void AutoCapturarAoCarregar()
    {
        EditorApplication.delayCall += () =>
        {
            Capturar();
            CapturarVisaoAerea();
        };
    }

    [MenuItem("Pac-Man Semântico/Capturar Câmera do Jogador PNG", false, 10)]
    public static void Capturar()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var cameras = Object.FindObjectsByType<Camera>();
            if (cameras.Length > 0) cam = cameras[0];
        }

        if (cam == null)
        {
            Debug.LogError("[CapturarCamera] Nenhuma câmera encontrada na cena.");
            return;
        }

        Vector3 posOriginal = cam.transform.position;
        Quaternion rotOriginal = cam.transform.rotation;

        // Ângulo panorâmico elevado para validar a topologia completa do labirinto e a gaiola central
        cam.transform.position = new Vector3(0f, 19f, -14f);
        cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

        int width = 1280;
        int height = 720;
        RenderTexture rt = new RenderTexture(width, height, 24);
        RenderTexture prevRt = cam.targetTexture;
        cam.targetTexture = rt;

        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        cam.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();

        cam.targetTexture = prevRt;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);

        cam.transform.position = posOriginal;
        cam.transform.rotation = rotOriginal;

        byte[] bytes = screenShot.EncodeToPNG();
        Object.DestroyImmediate(screenShot);

        string outputPath = Path.Combine(Application.dataPath, "../CapturaGameView.png");
        File.WriteAllBytes(outputPath, bytes);
        Debug.Log($"<color=#00FF99>[CapturarCamera] Salvo com sucesso em {outputPath}</color>");
    }

    [MenuItem("Pac-Man Semântico/Capturar Visão Aérea do Labirinto PNG", false, 11)]
    public static void CapturarVisaoAerea()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var cameras = Object.FindObjectsByType<Camera>();
            if (cameras.Length > 0) cam = cameras[0];
        }

        if (cam == null) return;

        Vector3 posOriginal = cam.transform.position;
        Quaternion rotOriginal = cam.transform.rotation;

        cam.transform.position = new Vector3(0f, 28f, -14f);
        cam.transform.rotation = Quaternion.Euler(62f, 0f, 0f);

        int width = 1280;
        int height = 720;
        RenderTexture rt = new RenderTexture(width, height, 24);
        RenderTexture prevRt = cam.targetTexture;
        cam.targetTexture = rt;

        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        cam.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();

        cam.targetTexture = prevRt;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);

        cam.transform.position = posOriginal;
        cam.transform.rotation = rotOriginal;

        byte[] bytes = screenShot.EncodeToPNG();
        Object.DestroyImmediate(screenShot);

        string outputPath = Path.Combine(Application.dataPath, "../CapturaVisaoAerea.png");
        File.WriteAllBytes(outputPath, bytes);
        Debug.Log($"<color=#00FF99>[CapturarCamera] Visão aérea salva em {outputPath}</color>");
    }
}
