#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Responsabilidad: Guarda el trabajo desde el editor; no se incluye en el ejecutable.
[InitializeOnLoad]
public static class AutoguardadoEditor
{

    #region Datos, configuracion y estado

    private const double Intervalo = 60d;

    private const string MenuActivado = "Herramientas/Autoguardado/Activado";

    private static string Clave => "AutoguardadoExcavadora." + Application.dataPath;

    private static bool Activado => EditorPrefs.GetBool(Clave, true);

    private static double proximoGuardado;

    #endregion

    #region Inicializacion y activacion

    static AutoguardadoEditor()
    {
        proximoGuardado = EditorApplication.timeSinceStartup + Intervalo;
        EditorApplication.update += Actualizar;
        EditorApplication.playModeStateChanged += AlCambiarModo;
    }

    #endregion

    #region Logica interna

    [MenuItem(MenuActivado)]
    private static void Alternar()
    {
        EditorPrefs.SetBool(Clave, !Activado);
        proximoGuardado = EditorApplication.timeSinceStartup + Intervalo;
    }

    [MenuItem(MenuActivado, true)]
    private static bool MostrarEstado()
    {
        Menu.SetChecked(MenuActivado, Activado);
        return true;
    }

    [MenuItem("Herramientas/Autoguardado/Guardar ahora")]
    private static void GuardarAhora()
    {
        Guardar();
    }

    private static void Actualizar()
    {
        if (Activado && EditorApplication.timeSinceStartup >= proximoGuardado)
            Guardar();
    }

    private static void AlCambiarModo(PlayModeStateChange estado)
    {
        if (Activado && estado == PlayModeStateChange.ExitingEditMode)
            Guardar(true);
    }

    private static void Guardar(bool antesDePlay = false)
    {
        if (EditorApplication.isPlaying ||
            (EditorApplication.isPlayingOrWillChangePlaymode && !antesDePlay) ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
            return;

        proximoGuardado = EditorApplication.timeSinceStartup + Intervalo;

        try
        {
            int guardadas = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene escena = SceneManager.GetSceneAt(i);
                // Las escenas nuevas necesitan guardarse manualmente una vez.
                if (!escena.isLoaded || !escena.isDirty ||
                    string.IsNullOrEmpty(escena.path) ||
                    EditorSceneManager.IsPreviewScene(escena))
                    continue;

                if (EditorSceneManager.SaveScene(escena))
                    guardadas++;
                else
                    Debug.LogWarning("No se pudo autoguardar: " + escena.path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Autoguardado: " + guardadas +
                " escena(s) guardada(s) y guardado de assets ejecutado.");
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
        }
    }

    #endregion

}
#endif
