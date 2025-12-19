using xdwModeBusClient.Tests;

namespace xdwModeBusClient;

/// <summary>
/// 测试运行器
/// </summary>
public static class TestRunner
{
    public static async Task RunTestsAsync()
    {
        var test = new ClientTest();
        await test.RunAllTestsAsync();
    }
}
