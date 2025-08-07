#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Inputic
{
    public class InputAssetsGlobalPostProcessor : AssetPostprocessor
    {
        enum InputicType
        {
            Bool,
            Trigger,
            Int,
            Float,
            Float2,
            Float3
        }

        private static readonly Regex re = new Regex(@"[^\w\d_]");

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            for (int i = 0; i < importedAssets.Length; i++)
            {
                var assetName = importedAssets[i];
                if (Path.GetExtension(assetName) != ".inputactions") continue;
                if (assetName.Contains("ignore")) continue;
                UpdateEnums(assetName);
            }
        }

        static string FixName(string name)
        {
            return re.Replace(name, "");
        }

        static bool TryDiscoverInputicType(InputAction c, out InputicType type)
        {
            switch (c.expectedControlType)
            {
                case "Button" when c.type == InputActionType.PassThrough:
                    type = InputicType.Trigger;
                    break;
                case "Button":
                    type = InputicType.Bool;
                    break;
                case "Axis":
                    type = InputicType.Float;
                    break;
                case "" when c.type == InputActionType.Button:
                    type = InputicType.Bool;
                    break;
                case "" when c.type == InputActionType.PassThrough:
                    type = InputicType.Trigger;
                    break;
                case "Vector2" or "Delta":
                    type = InputicType.Float2;
                    break;
                case "Vector3":
                    type = InputicType.Float3;
                    break;

                default:
                    type = InputicType.Int;
                    return false;
            }

            return true;
        }

        private static void UpdateEnums(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
            var assetName = FixName(asset.name);
            using var ie = asset.GetEnumerator();
            var constructors = new List<string>();
            var component = new List<string>();
            while (ie.MoveNext())
            {
                var c = ie.Current;
                if (!TryDiscoverInputicType(c, out var type)) continue;
                switch (type)
                {
                    case InputicType.Bool:
                    {
                        CreateBool(c, out var constructor, out var componentField);
                        constructors.Add(constructor);
                        component.Add(componentField);

                        break;
                    }
                    case InputicType.Trigger:
                    {
                        CreateTrigger(c, out var constructor, out var componentField);
                        constructors.Add(constructor);
                        component.Add(componentField);
                        break;
                    }
                    case InputicType.Float:
                    {
                        CreateAxis(c, out var constructor, out var componentField);
                        constructors.Add(constructor);
                        component.Add(componentField);
                        break;
                    }
                    case InputicType.Float2:
                    {
                        CreateFloat2(c, out var constructor, out var componentField);
                        constructors.Add(constructor);
                        component.Add(componentField);
                        break;
                    }
                    case InputicType.Float3:
                    {
                        CreateFloat3(c, out var constructor, out var componentField);
                        constructors.Add(constructor);
                        component.Add(componentField);
                        break;
                    }
                    case InputicType.Int:
                    {
                        CreateInt(c, out var constructor, out var componentField);
                        constructors.Add(constructor);
                        component.Add(componentField);
                        break;
                    }
                }
            }

            var template = $@"
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Inputic
{{
    public partial struct {assetName} : IComponentData
    {{
       {string.Join("\n", component)}
    }}
public class {assetName}Wrapper : IAbstractInputicInputAssetWrapper
    {{
        private InputActionAsset _actions;
        private {assetName} _component;
        private Entity _entity;
        private Action _delayedCleanup;
        private bool _enabled = false;
        public bool hasUpdates {{ get; private set; }}

        public {assetName}Wrapper (Entity entity, InputActionAsset actions)
        {{
            _enabled = false;
            _actions = actions;
            _entity = entity;
            {string.Join("\n", constructors)}
        }}


        private void Bump()
        {{
            hasUpdates = true;
        }}

        public void Enable()
        {{
            _actions.Enable();
            _enabled = true;
        }}

        public void Disable()
        {{
            _actions.Disable();
            _component = default;
            _delayedCleanup = null;
            _enabled = false;
        }}

        public void Toggle(bool value)
        {{
            if (value && !_enabled) Enable();
            else if (!value && _enabled) Disable();
        }}

        public void Cleanup()
        {{
        }}

        public void Update(EntityManager entityManager)
        {{
            if (!_enabled || !hasUpdates) return;
            hasUpdates = false;
            entityManager.SetComponentData(_entity, _component);
            _delayedCleanup?.Invoke();
            _delayedCleanup = null;
        }}
    }}
    [CreateAssetMenu(menuName = ""Inputic/{assetName}"")]
    public class {assetName}AssetSo  : AbstractInputicSharedInputAssetSo
    {{
        public override void UpdateBakedEntity(IBaker baker, Entity entity)
        {{
            baker.AddComponent(entity, new {assetName}());
        }}

        public override IAbstractInputicInputAssetWrapper CreateWrapper(Entity entity)
        {{
            return new {assetName}Wrapper(entity, actions);
        }}
    }}
}}
";
            WriteToFile(template, "Inputic/GENERATED/", $"{assetName}AssetSo.cs", true);
            EditorApplication.delayCall += () => { };
        }

        private static void CreateBool(InputAction inputAction, out string constructor, out string component)
        {
            var key = inputAction.name;
            var field = FixName(inputAction.name);
            constructor = $@"
            _actions[""{key}""].started += (ctx) =>
            {{
                _component.{field}.option = Button.Options.Started;
                Bump();
            }};
            _actions[""{key}""].performed += (ctx) =>
            {{
                _component.{field}.option = Button.Options.Performed;
                _delayedCleanup += () =>
                {{
                    Bump();
                    _component.{field}.option = Button.Options.Hold;
                }};
                Bump();
            }};
            _actions[""{key}""].canceled += (ctx) =>
            {{
                _component.{field}.option = Button.Options.Canceled;
                _delayedCleanup += () =>
                {{
                    Bump();
                    _component.{field}.option = 0;
                }};
                Bump();
            }};
";
            component = $" public Button {field};";
        }

        private static void CreateTrigger(InputAction inputAction, out string constructor, out string component)
        {
            var key = inputAction.name;
            var field = FixName(inputAction.name);
            constructor = $@"
          
            _actions[""{key}""].performed += (ctx) =>
            {{
                _component.{field}.triggered = 1;
                _delayedCleanup += () =>
                {{
                    Bump();
                    _component.{field}.triggered = 0;
                }};
                Bump();
            }};
           
";
            component = $" public Trigger {field};";
        }

        private static void CreateAxis(InputAction inputAction, out string constructor, out string component)
        {
            var key = inputAction.name;
            var field = FixName(inputAction.name);
            constructor = $@"
          
            _actions[""{key}""].performed += (ctx) =>
            {{
                _component.{field} = ctx.ReadValue<float>();
                Bump();
            }};
            _actions[""{key}""].canceled += (ctx) =>
            {{
                _component.{field} = 0;
                Bump();
            }};
";
            component = $" public float {field};";
        }       
        private static void CreateInt(InputAction inputAction, out string constructor, out string component)
        {
            var key = inputAction.name;
            var field = FixName(inputAction.name);
            constructor = $@"
          
            _actions[""{key}""].performed += (ctx) =>
            {{
                _component.{field} = ctx.ReadValue<int>();
                Bump();
            }};
            _actions[""{key}""].canceled += (ctx) =>
            {{
                _component.{field} = 0;
                Bump();
            }};
";
            component = $" public int {field};";
        }

        private static void CreateFloat2(InputAction inputAction, out string constructor, out string component)
        {
            var key = inputAction.name;
            var field = FixName(inputAction.name);
            constructor = $@"
          
            _actions[""{key}""].performed += (ctx) =>
            {{
                _component.{field} = ctx.ReadValue<Vector2>();
                Bump();
            }};
            _actions[""{key}""].canceled += (ctx) =>
            {{
                _component.{field} = float2.zero;
                Bump();
            }};
";
            component = $" public float2 {field};";
        }       
        private static void CreateFloat3(InputAction inputAction, out string constructor, out string component)
        {
            var key = inputAction.name;
            var field = FixName(inputAction.name);
            constructor = $@"
          
            _actions[""{key}""].performed += (ctx) =>
            {{
                _component.{field} = ctx.ReadValue<Vector3>();
                Bump();
            }};
            _actions[""{key}""].canceled += (ctx) =>
            {{
                _component.{field} = float3.zero;
                Bump();
            }};
";
            component = $" public float3 {field};";
        }

        public static void WriteToFile(string content, string path, string filename, bool overr = true)
        {
            var dirPath = Path.Join(Application.dataPath, path);
            var savePath = Path.Join(dirPath, filename);
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            if (File.Exists(savePath) && !overr) return;

            AssetDatabase.MakeEditable(savePath);
            File.WriteAllText(savePath, content);
        }
    }
}
#endif