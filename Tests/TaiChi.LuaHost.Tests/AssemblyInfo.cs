using Xunit;

// 为避免 LuaState / 原生资源在测试并行时产生偶发现象，禁用本测试程序集的并行执行。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
