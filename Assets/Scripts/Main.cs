using UnityEngine;
using YooAsset;

namespace  GameExamlpe
{
    public class Main : MonoBehaviour
    {
    
        private void Awake()
        {
            var handle = YooAssets.GetPackage("DefaultPackage");
            var game = handle.LoadAssetSync<GameObject>("Assets/HotGameObject/MainHotGameObject");
            game.InstantiateSync();
        }
    }
}

