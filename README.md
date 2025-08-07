The state system in DOTS is not the simplest concept to implement.
I need the ability to externally and controllably change the current state, and achieving this within a burst-compatible DOTS setup is rather complex.
This implementation puts all state change requests into a queue (pool) and processes them sequentially, which partially solves the problem.

To start, generate the code using the menu: Tools > FSM > Create new FSM.
It will generate four files:

$NAME$.entry.cs – the main file connected to the code generator; can be edited.

$NAME$.system.cs – the generated system file; can be edited.

$NAME$Authoring.cs – the generated authoring component for a minimal set of states; can be edited.

$NAME$.g.cs – contains jobs and utility code; editing is not recommended.

The entry file contains a number of structures:

```csharp
    [AutoFsm]
    public partial struct TestState1Handles
    {


    }

    public partial struct TestState1Context
    {
        public TestState1Context(ref SystemState state) : this()
        {
            
        }

        public void Update(ref SystemState state)
        {
            
        }
    }

    [PolymorphicStruct]
    public partial struct DemoState1:ITestState1
    {
        public bool ValidateExternalTransition(PolyTestState1 toState)=>true;
        public void OnEnter(ref TestState1HandlesWrapper data, ref TestState1Context ctx, PolyTestState1 merge, int i){}
        public void OnUpdate(ref TestState1HandlesWrapper data, ref TestState1Context ctx, ref DynamicBuffer<PolyTestState1> states, int i){}
        public void OnExit(ref TestState1HandlesWrapper data, ref TestState1Context ctx, int i){}
    }

```

TestState1Handles – the main structure required for code generation and retrieving data from the chunk inside state code.

TestState1Context – the state context, usually containing additional lookups or references to static data.

DemoState1 – an example of a state implementation.

ValidateExternalTransition – validates a transition from another state into this one. This validation is only triggered when the state is changed externally.

OnEnter – called when entering the state. merge is the data passed during the transition. In 99% of cases, merge is not needed and is used only for special states that have initial
settings or runtime configurations.

```csharp
    public partial struct TestState1Context {
        public ComponentTypeHandle<LocalTransform> transformHandle;
        [ReadOnly] public ComponentTypeHandle<LocalTransform> transformRoHandle;
        public BufferTypeHandle<LinkedEntityGroup> legHandle;
    }

```

Based on this, code generation will provide methods in TestState1HandlesWrapper:

```csharp
var transform = data.TransformHandleRo(i);
ref var transform = ref data.TransformHandleRw(i);

```

...and so on.

For transitions:

```csharp
FSMUtility.Transit(ref states, new DemoState1(){})
```