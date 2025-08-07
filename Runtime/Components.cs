using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Inputic
{
    //sss
    public partial struct InputicRegisterSharedInputAsset : IComponentData
    {
        public UnityObjectRef<AbstractInputicSharedInputAssetSo> value;
    }

    public partial struct InputicEnabledInput : IComponentData, IEnableableComponent
    {
    }

    public partial struct InputicRegistered : IComponentData
    {
        public int3 id;
    }

    public partial struct Button
    {
        [Flags]
        public enum Options : byte
        {
            Started = 1 << 0,
            Performed = 1 << 1,
            Canceled = 1 << 2,
            Hold = 1 << 3,
        }

        public bool isDown => option == Options.Performed;
        public bool isUp => option == Options.Canceled;
        public bool isHold => option == Options.Hold;
        public Options option;
    }

    public partial struct Trigger
    {
        public byte triggered;
        public bool isTriggered => triggered == 1;
    }
}