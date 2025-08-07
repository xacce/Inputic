using Unity.Entities;
using UnityEngine;

namespace Inputic
{
    //ss
    [DisallowMultipleComponent]
    public class RegisterSharedInputAssetAuthoring : MonoBehaviour
    {
        [SerializeField] private AbstractInputicSharedInputAssetSo asset_s;

        class _ : Baker<RegisterSharedInputAssetAuthoring>
        {
            public override void Bake(RegisterSharedInputAssetAuthoring authoring)
            {
                if (authoring.asset_s == null)
                {
                    Debug.LogError($"Empty inputic authoring component {authoring.gameObject}", authoring.gameObject);
                    return;
                }

                var e = GetEntity(TransformUsageFlags.None);
                authoring.asset_s.UpdateBakedEntity(this, e);
                AddComponent(e, new InputicRegisterSharedInputAsset
                {
                    value = authoring.asset_s,
                });
                AddComponent<InputicEnabledInput>(e);
                SetComponentEnabled<InputicEnabledInput>(e, true);
            }
        }
    }
}