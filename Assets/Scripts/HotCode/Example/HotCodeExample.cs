using TMPro;
using UnityEngine;
using YooAsset;

namespace GameExamlpe
{
    public class HotCodeExample : MonoBehaviour
    {
        private GameObject _player;

        private TMP_Text _text;


        private void Awake()
        {
            var package = YooAssets.GetPackage("DefaultPackage");
            var playerHandle = package.LoadAssetSync<GameObject>("Assets/Player/player");
            var textHandle = package.LoadAssetSync("Assets/UI/Text");
            _player = playerHandle.InstantiateSync();
            Transform parent = GameObject.Find("Canvas").transform;
            _text = textHandle.InstantiateSync(parent).GetComponent<TMP_Text>();
            _text.text += "   add hotcode";
        }


        private void Update()
        {
            _player.transform.Rotate(transform.forward);
        }
    }
}

