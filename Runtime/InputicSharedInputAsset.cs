using System;
using AutoId;
using Unity.Entities;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.InputSystem;

namespace Inputic
{
    public interface IAbstractInputicInputAssetWrapper
    {
        void Update(EntityManager entityManager);
        void Toggle(bool enabled);
    }


    public abstract class AbstractInputicSharedInputAssetSo : BaseAutoIdSo
    {
        [SerializeField] private InputActionAsset actions_s;

        public abstract IAbstractInputicInputAssetWrapper CreateWrapper(Entity entity);
        public InputActionAsset actions => actions_s;
        public abstract void UpdateBakedEntity(IBaker p, Entity entity);
        public override Type GetIdGroupType()
        {
            return typeof(AbstractInputicSharedInputAssetSo);
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (actions_s == null)
            {
                Debug.LogError($"Inputic asset {name} has no actions", this);
            }
        }
#endif
    }
}