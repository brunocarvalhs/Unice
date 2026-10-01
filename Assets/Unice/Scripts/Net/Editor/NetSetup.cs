using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Unice
{
    /// <summary>Creates the player network object (Resources/UniceNet/NetPlayer.prefab) once the multiplayer packages are installed.</summary>
    [InitializeOnLoad]
    static class NetSetup
    {
        const string Dir = "Assets/Unice/Resources/UniceNet", PlayerPath = Dir + "/NetPlayer.prefab";

        static NetSetup() { EditorApplication.delayCall += () => Ensure(false); }

        [MenuItem("Unice/Recriar objeto de rede")]
        static void Force() => Ensure(true);

        static void Ensure(bool force)
        {
            if (!force && File.Exists(PlayerPath)) return;
            Directory.CreateDirectory(Dir);
            var go = new GameObject("NetPlayer");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetPlayer>();
            PrefabUtility.SaveAsPrefabAsset(go, PlayerPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log("[Unice] Objeto de rede do jogador criado em " + PlayerPath);
        }
    }
}
