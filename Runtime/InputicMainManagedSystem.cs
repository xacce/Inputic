using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using XacceBasicSystemGroups;

namespace Inputic
{
#if !INPUTIC_DISABLE_AUTO_START
    [UpdateInGroup(typeof(PreInitializationSystemGroup))]
#else
    [DisableAutoCreation]
#endif
    public partial class InputicSystem : SystemBase
    {
        private Dictionary<int3, IAbstractInputicInputAssetWrapper> _assets;
        private EntityQuery _unregistered;
        private EntityQuery _stateChangedQuery;

        protected override void OnCreate()
        {
            _assets = new Dictionary<int3, IAbstractInputicInputAssetWrapper>();
            _unregistered = new EntityQueryBuilder(Allocator.Temp).WithAll<InputicRegisterSharedInputAsset>().Build(this);
            _stateChangedQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<InputicRegistered>().WithPresent<InputicEnabledInput>().Build(this);
            _stateChangedQuery.SetChangedVersionFilter(typeof(InputicEnabledInput));
            base.OnCreate();
        }

        protected override void OnDestroy()
        {
            if (_assets != null)
            {
                foreach (var asset in _assets.Values)
                {
                    asset.Toggle(false);
                }
            }

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(EntityManager.WorldUnmanaged);
            var unregisteredEntities = _unregistered.ToEntityArray(Allocator.Temp);
            foreach (var entity in unregisteredEntities)
            {
                var data = EntityManager.GetComponentData<InputicRegisterSharedInputAsset>(entity);
                var inputAsset = data.value.Value;
                if (!_assets.ContainsKey(inputAsset.id))
                {
                    var id = new int3(inputAsset.id, entity.Index, entity.Version);
                    var wrapper = inputAsset.CreateWrapper(entity);
                    wrapper.Toggle(true);
                    _assets.Add(id, wrapper);
                    ecb.AddComponent(entity, new InputicRegistered { id = id });
                    ecb.RemoveComponent<InputicRegisterSharedInputAsset>(entity);
                }
                else
                {
                    Debug.LogError($"[Inputic] Asset with id {inputAsset.id} already registered, but {entity} trying to register again");
                }
            }

            var stateChangedEntities = _stateChangedQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in stateChangedEntities)
            {
                var enabled = EntityManager.IsComponentEnabled<InputicEnabledInput>(entity);
                var id = EntityManager.GetComponentData<InputicRegistered>(entity).id;
                Debug.Log($"[Inputic] Detected state changed for entity {entity} with id {id} to enabled: {enabled}");
                if (_assets.ContainsKey(id))
                {
                    _assets[id].Toggle(enabled);
                }
                else
                {
                    Debug.LogError($"[Inputic] Could not find registered inputic asset with id {id}");
                }
            }

            foreach (var asset in _assets.Values)
            {
                asset.Update(EntityManager);
            }
        }
    }
}