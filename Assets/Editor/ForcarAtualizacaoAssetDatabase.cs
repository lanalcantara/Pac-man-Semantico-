using UnityEditor;
using UnityEngine;

public static class ForcarAtualizacaoAssetDatabase
{
    [MenuItem("Pac-Man Semântico/Recompilar e Atualizar Scripts")]
    public static void Atualizar()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        Debug.Log("<color=#00FF99>[VirtOnto]</color> AssetDatabase atualizado e recompilação solicitada com sucesso!");
    }
}
