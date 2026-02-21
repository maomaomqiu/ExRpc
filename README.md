# ExRpc

### 概述

ExRpc 是一个分布式远程过程调用（RPC）框架，专为在 .NET 生态系统中构建可扩展的集群微服务而设计。它提供强大的服务发现、负载均衡、事件驱动消息传递和自动代码生成功能。

### 核心特性

- **RPC 通信**：基于 TCP 的客户端-服务器请求/响应消息传递
- **集群管理**：集成 ZooKeeper 进行服务发现和协调
- **负载均衡**：支持多种集群模式
  - Master-Slave（主从模式，支持故障转移）
  - Cluster（基于取模的路由）
  - ClusterWithHash（一致性哈希分布）
- **事件驱动架构**：RabbitMQ 集成，支持发布/订阅消息传递
- **代码生成**：从协议定义自动生成代理类
- **事务管理**：带重试逻辑的异步事务处理
- **连接池**：高效的连接管理和自动重连
- **性能监控**：内置 RPC 调用性能指标跟踪

### 项目结构

```
ExRpc/
├── ExRpc.Common/          # 核心 RPC 框架 (v1.1.4)
├── ExRpc.Messaging/       # 消息和事件系统 (v1.0.2)
├── CodeMaker/             # .NET Framework 代码生成工具
├── CodeMakerCore/         # .NET Core 代码生成工具
├── UnitTest/              # 单元测试
└── ExRpc.sln             # 解决方案文件
```

### 技术栈

- **.NET Standard 2.0/2.1** - 跨平台兼容的核心库
- **ZooKeeperNetEx** (v3.4.12.4) - 服务发现和集群协调
- **RabbitMQ.Client** (v6.2.1) - 消息队列代理集成
- **NetEZ 框架** - 内部网络和序列化库

### 快速开始

#### 前置要求

- .NET Standard 2.0 或更高版本
- ZooKeeper 服务器（用于集群模式）
- RabbitMQ 服务器（用于事件消息传递，可选）

#### 服务端使用

```csharp
// 1. 定义服务接口
public interface IMyService : IServant
{
    string HelloWorld(string name);
}

// 2. 实现服务
public class MyServiceImpl : IMyService
{
    public string HelloWorld(string name)
    {
        return $"Hello, {name}!";
    }
}

// 3. 创建并启动服务器
var config = new ServerHostConfigure
{
    Host = "localhost",
    Port = 9000,
    GridUri = "rpc://mycluster.myproject.root"
};

var server = new RPCServerHost(config);
server.RegisterServant(new MyServiceImpl());
server.Start();
```

#### 客户端使用

```csharp
// 1. 创建通信器并配置
var config = new CommunicatorConfigure
{
    ConnectTimeout = 200,
    SendTimeout = 3000,
    WaitingACKTimeout = 9000
};

var communicator = new Communicator(config);

// 2. 使用生成的代理调用远程服务
var proxy = new MyServiceProxy(communicator, "rpc://mycluster.myproject.root");
var result = await proxy.HelloWorldAsync("World");
Console.WriteLine(result); // 输出：Hello, World!
```

#### 代码生成

使用代码生成工具从协议定义自动创建代理类：

```bash
# .NET Core 版本
dotnet CodeMakerCore.dll --assembly YourProtocol.dll --output ./Generated

# .NET Framework 版本
CodeMaker.exe --assembly YourProtocol.dll --output ./Generated
```

### 配置

#### ZooKeeper 配置

在 `c:\xconfig2\zookeeper.xml` 中配置 ZooKeeper 连接：

```xml
<Configuration>
    <ZooKeeper>
        <ConnectionString>localhost:2181</ConnectionString>
        <SessionTimeout>10000</SessionTimeout>
    </ZooKeeper>
</Configuration>
```

#### RabbitMQ 配置

在 `rabbitmq.xml` 中配置 RabbitMQ：

```xml
<Configuration>
    <RabbitMQ>
        <HostName>localhost</HostName>
        <Port>5672</Port>
        <UserName>guest</UserName>
        <Password>guest</Password>
    </RabbitMQ>
</Configuration>
```

### 集群模式

#### GridUri 格式

```
rpc://[集群模式].[项目名].[根名称]
```

- **None**：单服务器实例
- **MasterSlave**：主/从模式，支持自动故障转移
- **Cluster**：多实例，基于取模的路由
- **ClusterWithHash**：基于一致性哈希的分布

### 事件消息传递

```csharp
// 订阅事件
var eventManager = new ExEventManager();
eventManager.Subscribe<MyEvent>("myqueue", async (evt) =>
{
    Console.WriteLine($"收到事件: {evt.Data}");
});

// 发布事件
eventManager.Publish(new MyEvent { Data = "Hello Events!" });
```

### 高级特性

#### 事务管理

- 带自动清理的异步回调
- 可配置的可靠性模式：
  - `MODE_SEND_CONSISTENCE`：确认发送成功
  - `MODE_SEND_WAITING_ACK`：等待服务器响应
- 自动事务过期（默认 60 秒）
- 可配置重试次数的重试逻辑（默认 2 次）

#### 性能监控

内置的 `PerformanceRecorder` 跟踪：
- RPC 调用持续时间
- 成功/失败次数
- 平均响应时间

### 许可证

请参考项目许可证文件获取许可信息。
