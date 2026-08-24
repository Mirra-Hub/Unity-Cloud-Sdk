using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class TestAddressable : MonoBehaviour
{
    [SerializeField] private string _assetKey;

    async void Start()
    {
        var prefab = Addressables.LoadAssetAsync<GameObject>(_assetKey);

        await prefab.Task;

        Instantiate(prefab.Result);
    }
    
}

