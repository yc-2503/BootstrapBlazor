﻿// Copyright (c) Argo Zhang (argo@163.com). All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Website: https://www.blazor.zone or https://argozhang.github.io/

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace UnitTest.Services;

public class ConnectionHubTest
{
    [Fact]
    public async Task Callback_Ok()
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddBootstrapBlazor();

        var options = context.Services.GetRequiredService<IOptions<BootstrapBlazorOptions>>();
        options.Value.ConnectionHubOptions = new()
        {
            Enable = true,
            ExpirationScanFrequency = TimeSpan.FromMicroseconds(200),
            BeatInterval = 500
        };

        var client = context.Services.GetRequiredService<WebClientService>();
        var service = context.Services.GetRequiredService<IConnectionService>();
        var cut = context.RenderComponent<ConnectionHub>();
        await cut.InvokeAsync(async () =>
        {
            client.SetData(new ClientInfo() { Id = "test_id", Ip = "::1" });
            await cut.Instance.Callback("test_id");
        });
        Assert.Equal(1, service.Count);

        // 触发 Beat 时间
        await Task.Delay(100);
        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.Callback("test_id");
        });
        Assert.True(service.TryGetValue("test_id", out var item));
        Assert.NotNull(item?.ClientInfo);
        Assert.True(item?.ConnectionTime < DateTimeOffset.Now);
        cut.Dispose();

        // 触发内部 ClientInfo 为空情况 覆盖 _clientInfo ??= new();
        options.Value.ConnectionHubOptions.Enable = false;
        client = context.Services.GetRequiredService<WebClientService>();
        cut = context.RenderComponent<ConnectionHub>();
        await cut.InvokeAsync(async () =>
        {
            client.SetData(new ClientInfo() { Id = "test_id", Ip = "::1" });
            await cut.Instance.Callback("test_id");
        });
    }

    [Fact]
    public async Task ExpirationScanFrequency_Ok()
    {
        var services = new ServiceCollection();
        services.AddBootstrapBlazor();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BootstrapBlazorOptions>>();
        options.Value.ConnectionHubOptions = new()
        {
            Enable = true,
            ExpirationScanFrequency = TimeSpan.FromMicroseconds(200),
            BeatInterval = 100
        };

        var service = provider.GetRequiredService<IConnectionService>();
        service.AddOrUpdate(new ClientInfo() { Id = "test_id" });
        Assert.Equal(1, service.Count);
        Assert.Single(service.Connections);

        await Task.Delay(200);
        Assert.Equal(0, service.Count);
    }

    [Fact]
    public async Task ExpirationScanFrequency_Cancel()
    {
        var type = Type.GetType("BootstrapBlazor.Components.DefaultConnectionService, BootstrapBlazor");
        Assert.NotNull(type);

        var service = Activator.CreateInstance(type, new BootstrapBlazorOptions()
        {
            ConnectionHubOptions = new()
            {
                Enable = true,
            }
        });
        Assert.NotNull(service);

        var fieldInfo = type.GetField("_cancellationTokenSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(fieldInfo);

        var token = fieldInfo.GetValue(service) as CancellationTokenSource;
        Assert.NotNull(token);

        await Task.Delay(100);
        token.Cancel();
    }

    [Fact]
    public void ConnectionHubOptions_Ok()
    {
        var services = new ServiceCollection();
        services.AddBootstrapBlazor();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IConnectionService>();
        service.AddOrUpdate(new ClientInfo() { Id = "test_id" });
        Assert.Equal(1, service.Count);
    }
}