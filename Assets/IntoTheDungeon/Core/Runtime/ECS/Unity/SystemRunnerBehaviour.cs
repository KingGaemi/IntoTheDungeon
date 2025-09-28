using UnityEngine;
using IntoTheDungeon.Core.Runtime.Runner;

[DefaultExecutionOrder(-9000)]
public sealed class SystemRunnerBehaviour : MonoBehaviour
{
    public SystemRunner Runner { get; private set; }
    public void Init(SystemRunner r) => Runner = r;

    void Update()      => Runner?.RunUpdate(Time.deltaTime);
    void FixedUpdate() => Runner?.RunFixed(Time.fixedDeltaTime);
    void LateUpdate()  => Runner?.RunLate(Time.deltaTime);
}
