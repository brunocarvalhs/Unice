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
            var playerObject = new GameObject("NetPlayer");
            playerObject.AddComponent<NetworkObject>();
            playerObject.AddComponent<NetPlayer>();
            PrefabUtility.SaveAsPrefabAsset(playerObject, PlayerPath);
            Object.DestroyImmediate(playerObject);
            AssetDatabase.SaveAssets();
            Debug.Log("[Unice] Objeto de rede do jogador criado em " + PlayerPath);
        }
    }
}
