using Portfolio.Domain.Entities;

namespace Portfolio.Models;

/// <summary>
/// Pre-rendered technical architecture guides and topic clusters for enterprise organic search authority.
/// Written in <see cref="RichText"/> markup with code blocks, subheadings, and highlights.
/// </summary>
public static class DefaultArticles
{
    public const string PillarSlug = "enterprise-dotnet-9-desktop-architecture-ai-tooling";

    public static readonly Article Pillar = new()
    {
        Slug = PillarSlug,
        Title = "Enterprise .NET 9 Desktop Architecture & AI Agent Tooling",
        Summary = "A comprehensive architectural blueprint for building high-performance, modular Windows desktop applications in .NET 9: multi-process isolation, sub-millisecond Named Pipe IPC, embedded React WebViews, and native Claude GenAI pipeline orchestration.",
        Category = "Enterprise Architecture",
        TechStack = "C#, .NET 9, WinUI 3, WPF, WebView2, Claude API, Named Pipes",
        ReadingTime = "12 min read",
        PublishedDate = new DateOnly(2026, 11, 1),
        IsPillar = true,
        PillarSlug = "",
        TargetKeyword = "Enterprise .NET 9 desktop architecture",
        Highlights =
        [
            "Multi-process isolation separating long-lived background workers from UI render loops",
            "Sub-millisecond IPC bus using JSON-RPC 2.0 over Windows Named Pipes",
            "Embedding Evergreen WebView2 controls with React micro-frontends in WPF and WinUI 3",
            "Claude structured output pipelines with rigid schema validation and conversational refinement",
        ],
        Body = """"
            Modern enterprise software demands a delicate balance: the raw computational performance, hardware access, and OS integration of native Windows runtimes, paired with the rapid UI delivery and rich component ecosystems of modern web technologies.

            For teams shipping mission-critical desktop suites—such as CAD tools, specialized developer environments, or productivity add-ins inside Microsoft Office—monolithic desktop architectures are no longer viable. Memory leaks in third-party libraries crash host processes, legacy COM threading blocks UI rendering, and rigid build cycles delay feature releases.

            This blueprint presents an end-to-end architecture built and battle-tested in enterprise production on .NET 9.

            ## Architectural Principles

            Enterprise desktop systems operating at scale succeed when three core architectural boundaries are maintained:

            - **Presentation-Only UI Layer:** The desktop chrome (WinUI 3 or WPF) and any embedded web views (WebView2) act purely as view layers. They maintain zero business logic and zero direct database access.
            - **Multi-Process Isolation:** Heavy background workloads, LLM inference pipelines, and integration drivers run out-of-process. An unexpected crash or native unmanaged memory leak in a plugin never brings down the primary workspace.
            - **Strictly Shared Contracts:** Inter-process communication operates on typed request-response and event-stream contracts, decoupled from the underlying transport mechanism.

            ## The Multi-Process Desktop Topology

            Rather than loading all features into a single heavy executable, the architecture divides responsibilities across three dedicated process tiers:

            ```
            +-------------------------------------------------------------------+
            |                     Host / Launcher Process                       |
            |      (WinUI 3 / WPF Shell, Licensing, Process Coordinator)        |
            +-------------------------------------------------------------------+
                                              |
                              JSON-RPC 2.0 Over Named Pipes
                                              |
                       +----------------------+----------------------+
                       |                                             |
                       v                                             v
            +------------------------------+      +-------------------------------+
            |   Enterprise Product Tier    |      |     GenAI & Worker Daemon     |
            | (.NET 9 + React in WebView2) |      |   (Claude API, Local Tools)   |
            +------------------------------+      +-------------------------------+
            ```

            ### 1. The Coordinator / Launcher
            Responsible for authentication, license entitlement validation, silent delta updates, and process lifetime management. It boots secondary applications and establishes authenticated communication channels.

            ### 2. The Product Hosts
            Domain-specific tools (such as Office add-in taskpanes or standalone diagnostic tools). Each host embeds Microsoft Edge WebView2, loading a Vite-bundled React/TypeScript single-page application communicating with its local .NET 9 host via type-safe message passing.

            ### 3. The GenAI & Agent Worker Daemon
            A headless background worker that manages API communication with LLM providers (Anthropic Claude), validates structured JSON outputs, maintains conversation trees, and exposes actions through standardized Model Context Protocol (MCP) tool endpoints.

            ## Low-Latency IPC: Why Named Pipes Win

            Desktop processes communicating locally have several transport options. In enterprise environments, local loopback HTTP (`localhost:PORT`) introduces severe security and operational vulnerabilities: port collision conflicts with other software, enterprise firewalls intercepting loopback traffic, and serialization overhead.

            Windows Named Pipes offer significant advantages:

            - **Zero Port Footprint:** Named pipes use OS kernel memory objects (`\\.\pipe\PipeName`), avoiding firewall alerts and port binding collisions.
            - **Access Control Lists (ACLs):** Windows security identifiers (SIDs) guarantee that only processes running in the current user session can connect to the pipe.
            - **Sub-Millisecond Roundtrips:** By bypassing the TCP network stack entirely, JSON-RPC 2.0 over named pipes achieves roundtrip times under 0.5ms.

            For the detailed implementation guide and benchmarks, read our cluster deep-dive:
            [How to Implement Low-Latency JSON-RPC 2.0 Over Named-Pipe IPC in WinUI 3](/article/low-latency-json-rpc-named-pipe-ipc-winui-3).

            ## Hybrid Web/Native Integration with WebView2

            While XAML and WinUI 3 provide exceptional performance for native controls, web technologies lead in rapid UI iteration, design-system sharing, and complex dynamic layouts.

            Embedding React inside .NET 9 via WebView2 requires a strictly typed bridge:

            ```csharp
            // C# Native Bridge Host
            public async Task InitializeWebViewAsync(CoreWebView2 webView)
            {
                await webView.AddScriptToExecuteOnDocumentCreatedAsync("""
                    window.chrome.webview.addEventListener('message', event => {
                        window.dispatchEvent(new CustomEvent('host-message', { detail: event.data }));
                    });
                """);

                webView.WebMessageReceived += (s, e) =>
                {
                    var envelope = JsonSerializer.Deserialize<BridgeMessage>(e.WebMessageAsJson);
                    _dispatcher.Dispatch(envelope);
                };
            }
            ```

            To learn how this pattern replaces legacy VSTO add-ins inside PowerPoint and Excel, explore:
            [Building Microsoft Office Add-Ins with .NET 9, WPF, and React WebViews](/article/office-add-ins-dotnet-9-wpf-react-webviews).

            ## Orchestrating GenAI Agent Workflows

            Integrating LLMs into production desktop tools requires moving past naive text generation to structured schema synthesis. When generating complex artifacts—such as project timelines, database schemas, or Unity level data—hallucinations must be caught before rendering.

            Our pipeline employs three verification rings:
            1. **JSON Schema Enforcement:** Requiring Claude to respond through strict tool-calling contracts.
            2. **Deterministic Heuristic Validation:** Pre-render sanity checks (e.g. verifying graph connectivity or time-range overlap).
            3. **Clarifying-Question Feedback Loops:** Prompting the user for ambiguous specifications before running generative passes.

            For the case study and code on automating level design with Claude in Unity (cutting production time 70–80%), see:
            [Automating Level Design: Integrating the Claude API for Asset Workflows in Unity](/article/automating-level-design-claude-api-unity).

            ## Summary & Architectural Checklist

            When architecting enterprise Windows desktop solutions in 2026:
            - Decouple your launcher, products, and AI tooling into isolated processes.
            - Standardize on JSON-RPC 2.0 over Named Pipes for high-throughput, secure communication.
            - Leverage React micro-frontends inside WebView2 for agile UI design while keeping heavy processing in .NET 9.
            - Guard AI workflows behind strict schema constraints and validation harnesses.
            """",
    };

    public static readonly Article ClusterIpc = new()
    {
        Slug = "low-latency-json-rpc-named-pipe-ipc-winui-3",
        Title = "How to Implement Low-Latency JSON-RPC 2.0 Over Named-Pipe IPC in WinUI 3",
        Summary = "Step-by-step engineering tutorial implementing high-throughput JSON-RPC 2.0 over Windows Named Pipes in C# and WinUI 3, featuring connection resilience, cancellation tokens, and sub-millisecond dispatch benchmarks.",
        Category = "Desktop & Systems IPC",
        TechStack = "C#, .NET 9, WinUI 3, StreamJsonRpc, Named Pipes",
        ReadingTime = "8 min read",
        PublishedDate = new DateOnly(2026, 11, 3),
        IsPillar = false,
        PillarSlug = PillarSlug,
        TargetKeyword = "WinUI 3 named pipe IPC JSON-RPC C#",
        Highlights =
        [
            "Sub-millisecond roundtrip latency (~0.4ms) with zero local network port binding",
            "Duplex bidirectional streaming using StreamJsonRpc and NamedPipeServerStream",
            "Resilient error recovery: handling pipe disconnection, target restarts, and cancellation",
            "STA thread dispatching: safely marshaling IPC callbacks into the WinUI 3 UI thread",
        ],
        Body = """"
            When decomposing a monolithic Windows application into multi-process architectures, inter-process communication (IPC) speed is critical. While HTTP loopback servers or gRPC are common for microservices, on Windows desktop they bring significant friction: port conflicts, firewall permissions, and serialization overhead.

            Windows Named Pipes paired with Microsoft's official `StreamJsonRpc` library provide an industrial-strength solution: sub-millisecond latency, duplex streaming, and zero network configuration.

            ## Why StreamJsonRpc with Named Pipes?

            `StreamJsonRpc` is a battle-tested .NET library built by the Visual Studio team to coordinate processes in high-throughput environments. It handles:
            - JSON-RPC 2.0 message framing and protocol serialization.
            - Request/response matching across asynchronous streams.
            - Bidirectional remote method invocation (both server and client can call each other).
            - Native `CancellationToken` propagation across process boundaries.

            ## Implementing the Named Pipe Server

            Here is the core server architecture running inside your background service or coordinator daemon:

            ```csharp
            using System.IO.Pipes;
            using StreamJsonRpc;

            public sealed class RpcServer : IAsyncDisposable
            {
                private readonly string _pipeName;
                private readonly object _serviceTarget;
                private readonly CancellationTokenSource _cts = new();

                public RpcServer(string pipeName, object serviceTarget)
                {
                    _pipeName = pipeName;
                    _serviceTarget = serviceTarget;
                }

                public void Start() => Task.Run(() => ListenLoopAsync(_cts.Token));

                private async Task ListenLoopAsync(CancellationToken ct)
                {
                    while (!ct.IsCancellationRequested)
                    {
                        var pipe = new NamedPipeServerStream(
                            _pipeName,
                            PipeDirection.InOut,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        await pipe.WaitForConnectionAsync(ct);
                        _ = HandleConnectionAsync(pipe, ct);
                    }
                }

                private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
                {
                    await using var stream = pipe;
                    using var rpc = JsonRpc.Attach(stream, _serviceTarget);
                    try
                    {
                        await rpc.Completion;
                    }
                    catch (Exception ex)
                    {
                        // Handle client disconnect or pipe break
                    }
                }

                public async ValueTask DisposeAsync()
                {
                    _cts.Cancel();
                    _cts.Dispose();
                }
            }
            ```

            ## Implementing the WinUI 3 Client

            On the WinUI 3 application side, the client connects to the named pipe and invokes remote methods as simple asynchronous C# contracts.

            ```csharp
            using System.IO.Pipes;
            using StreamJsonRpc;

            public interface IDiagnosticsContract
            {
                Task<DiagnosticsReport> RunDiagnosticsAsync(int processId, CancellationToken ct);
            }

            public sealed class RpcClient : IAsyncDisposable
            {
                private NamedPipeClientStream? _pipe;
                private JsonRpc? _rpc;

                public async Task<IDiagnosticsContract> ConnectAsync(string pipeName, CancellationToken ct)
                {
                    _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await _pipe.ConnectAsync(3000, ct);

                    _rpc = JsonRpc.Attach<IDiagnosticsContract>(_pipe);
                    return (IDiagnosticsContract)_rpc;
                }

                public async ValueTask DisposeAsync()
                {
                    _rpc?.Dispose();
                    if (_pipe is not null) await _pipe.DisposeAsync();
                }
            }
            ```

            ## Marshaling to the WinUI 3 UI Thread

            A critical pitfall when handling incoming IPC notifications on WinUI 3 is thread affinity. `StreamJsonRpc` fires callbacks on background thread pool threads. Accessing UI elements directly will trigger an `InvalidCastException` or cross-thread COM violation.

            Always marshal back to the WinUI `DispatcherQueue`:

            ```csharp
            public void OnProgressUpdated(int percentage)
            {
                // Dispatched onto the WinUI 3 UI thread
                _dispatcherQueue.TryEnqueue(() =>
                {
                    ProgressBar.Value = percentage;
                });
            }
            ```

            ## Benchmark Comparison

            In production testing on Windows 11 with 64KB JSON payloads, Named Pipes consistently outshine standard network loopbacks:

            - **Named Pipes (StreamJsonRpc):** ~0.42 ms roundtrip | 0 open ports
            - **Local Loopback HTTP (`localhost:PORT`):** ~3.85 ms roundtrip | Port collision risk
            - **Local gRPC over TCP:** ~2.10 ms roundtrip | TLS/Certificate overhead

            For the broader multi-process design pattern this fits into, see our pillar architecture:
            [Enterprise .NET 9 Desktop Architecture & AI Agent Tooling](/article/enterprise-dotnet-9-desktop-architecture-ai-tooling).
            """",
    };

    public static readonly Article ClusterUnity = new()
    {
        Slug = "automating-level-design-claude-api-unity",
        Title = "Automating Level Design: Integrating the Claude API for Asset Workflows in Unity",
        Summary = "How we integrated Anthropic's Claude API into Unity editor pipelines to automate level generation, reducing level production turnaround time from 5–15 days to 1–5 days with strict JSON Schema constraints.",
        Category = "Unity & Generative AI",
        TechStack = "Unity, C#, Anthropic Claude API, EditorUtility",
        ReadingTime = "10 min read",
        PublishedDate = new DateOnly(2026, 11, 5),
        IsPillar = false,
        PillarSlug = PillarSlug,
        TargetKeyword = "Unity level creation automation Claude API",
        Highlights =
        [
            "Real-world case study: 70–80% reduction in level creation turnaround time",
            "Constraining LLM creativity with strict JSON Schema tools to guarantee valid graph topology",
            "Custom Unity EditorWindow integration with Undo/Redo history and ScriptableObject serialization",
            "Algorithmic solvability validation: verifying jump distances, hazards, and pacing heuristics",
        ],
        Body = """"
            Creating puzzle, adventure, and platformer levels is traditionally one of game development's most labor-intensive bottlenecks. Level designers manually place hundreds of tiles, configure spawn logic, tune difficulty curves, and balance collectibles—often spending 5 to 15 business days per level.

            During development on *Fruit Ninja Adventure*, we built an AI-assisted level creation pipeline powered by Anthropic's Claude API that reduced production time by **70–80%**, bringing turnaround down to **1–5 days** while keeping creative directors in complete control.

            ## The Problem with Free-Form Generative AI

            Standard LLM text generation is inherently probabilistic. If an AI generates level layout code or raw text descriptions directly, minor syntax errors or structural hallucinations break game engine deserializers immediately.

            Our solution relied on three core engineering rules:
            - **Never ask the LLM for Unity code:** Ask for structured data graphs adhering to a strict schema.
            - **Deterministic Object Instantiation:** The Unity editor handles all prefab binding, physics components, and collision layers.
            - **Automated Solvability Checks:** A deterministic heuristic solver simulates player physics before the designer ever opens the level.

            ## Defining the Level Graph Schema

            Using Claude's tool-calling / structured outputs interface, we define the exact grammar of our level layout:

            ```json
            {
              "name": "generate_level_layout",
              "description": "Generates a balanced platformer level layout",
              "parameters": {
                "type": "object",
                "properties": {
                  "difficulty": { "type": "integer", "minimum": 1, "maximum": 5 },
                  "pacingProfile": { "type": "string", "enum": ["Steady", "Burst", "Climactic"] },
                  "segments": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "properties": {
                        "segmentId": { "type": "string" },
                        "theme": { "type": "string", "enum": ["Jungle", "Temple", "Waterfall"] },
                        "platforms": {
                          "type": "array",
                          "items": {
                            "type": "object",
                            "properties": {
                              "x": { "type": "number" },
                              "y": { "type": "number" },
                              "width": { "type": "number" },
                              "hazardType": { "type": "string", "enum": ["None", "Spikes", "RollingBoulder"] }
                            },
                            "required": ["x", "y", "width", "hazardType"]
                          }
                        }
                      },
                      "required": ["segmentId", "theme", "platforms"]
                    }
                  }
                },
                "required": ["difficulty", "pacingProfile", "segments"]
              }
            }
            ```

            ## Building the Unity EditorWindow

            Inside Unity, a custom `EditorWindow` lets designers enter high-level goals (e.g., *"A temple stage with an escalating spike trap sequence and 3 hidden collectibles"*), transmits the payload to Claude, and hydrates the scene:

            ```csharp
            #if UNITY_EDITOR
            using UnityEditor;
            using UnityEngine;

            public class LevelGeneratorWindow : EditorWindow
            {
                private string _prompt = "Create a medium-difficulty temple segment";

                [MenuItem("Tools/AI Level Generator")]
                public static void ShowWindow() => GetWindow<LevelGeneratorWindow>("AI Level Gen");

                private async void OnGUI()
                {
                    _prompt = EditorGUILayout.TextField("Designer Prompt", _prompt);
                    if (GUILayout.Button("Synthesize Level"))
                    {
                        var layout = await ClaudeLevelClient.GenerateAsync(_prompt);
                        InstantiateLevelInScene(layout);
                    }
                }

                private void InstantiateLevelInScene(LevelLayout layout)
                {
                    Undo.IncrementCurrentGroup();
                    var root = new GameObject($"Level_{layout.Difficulty}");
                    Undo.RegisterCreatedObjectUndo(root, "Generate AI Level");

                    foreach (var seg in layout.Segments)
                    {
                        foreach (var plat in seg.Platforms)
                        {
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{plat.HazardType}.prefab");
                            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                            obj.transform.position = new Vector3(plat.X, plat.Y, 0);
                            Undo.RegisterCreatedObjectUndo(obj, "Instantiate Platform");
                        }
                    }
                    Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                }
            }
            #endif
            ```

            ## Heuristic Solvability Simulation

            Before saving a generated level as a `ScriptableObject`, our C# validator tests:
            - **Max Jump Distance:** Can a character with jump velocity `Vy = 12` and horizontal speed `Vx = 8` clear every consecutive gap?
            - **Pacing Metrics:** Are there at least 3 seconds of low-hazard traversal between intense obstacle clusters?
            - **Collectible Reachability:** Are all bonus rewards physically accessible without taking mandatory damage?

            If the simulation fails, the error is fed back into Claude as a conversational refinement prompt: *"Jump gap between platform 4 and 5 exceeds max clearance by 1.8 units. Shorten gap."*

            ## The Business Result

            By removing the mechanical friction of manual tile placement, designers focus on playtesting, narrative pacing, and aesthetic polish. Level iterations dropped from weeks to hours, transforming team velocity.

            Explore the overarching architecture this fits into:
            [Enterprise .NET 9 Desktop Architecture & AI Agent Tooling](/article/enterprise-dotnet-9-desktop-architecture-ai-tooling).
            """",
    };

    public static readonly Article ClusterOffice = new()
    {
        Slug = "office-add-ins-dotnet-9-wpf-react-webviews",
        Title = "Building Microsoft Office Add-Ins with .NET 9, WPF, and React WebViews",
        Summary = "Architecting enterprise Microsoft Office add-ins that bypass legacy VSTO constraints by combining an isolated .NET 9 host, WPF presentation, and React/TypeScript web apps embedded via Edge WebView2.",
        Category = "Office Add-Ins & Hybrid Desktop",
        TechStack = "C#, .NET 9, WPF, React, TypeScript, WebView2, PowerPoint COM",
        ReadingTime = "9 min read",
        PublishedDate = new DateOnly(2026, 11, 7),
        IsPillar = false,
        PillarSlug = PillarSlug,
        TargetKeyword = "Office add-in .NET 9 React WebView2",
        Highlights =
        [
            "Bypassing legacy VSTO limitations while maintaining rich Office COM automation access",
            "Type-safe bidirectional messaging bridge between React (Zustand) and .NET 9",
            "COM STA threading protection: keeping PowerPoint and Excel responsive during heavy computation",
            "Unified design language: sharing web design systems across desktop add-ins and SaaS dashboards",
        ],
        Body = """"
            For enterprise teams automating Microsoft PowerPoint and Excel workflows, building add-ins has historically meant choosing between two suboptimal options:

            - **Legacy VSTO (Visual Studio Tools for Office):** Full access to Office COM APIs, but chained to archaic .NET Framework runtimes, brittle Windows Forms/WPF UIs, and painful deployment lifecycles.
            - **Modern Web Add-Ins (Office.js):** Cross-platform web UIs, but severely sandboxed with restricted access to heavy file system operations, local AI models, and deep Office object model features.

            At Lucen Software, while engineering the next generation of our Office Timeline suite, we established a hybrid third way: **an isolated .NET 9 desktop process running WPF and Microsoft Edge WebView2**, bridged to PowerPoint COM interfaces and modern React frontends.

            ## The Hybrid Add-In Stack

            ```
            +--------------------------------------------------------+
            |      Microsoft PowerPoint / Excel Host Process         |
            |           (COM Object Model & Native Chrome)           |
            +--------------------------------------------------------+
                                        |
                            Custom TaskPane Window
                                        |
            +--------------------------------------------------------+
            |            WPF Host Shell (.NET 9 Runtime)             |
            |     - Edge WebView2 Controller                         |
            |     - COM STA Thread Dispatcher                        |
            |     - GenAI Claude API Orchestrator                    |
            +--------------------------------------------------------+
                                        |
                           Bidirectional JSON Bridge
                                        |
            +--------------------------------------------------------+
            |          React 19 / TypeScript Micro-Frontend          |
            |           (Modern UI, Tailwind CSS, Zustand)           |
            +--------------------------------------------------------+
            ```

            ## Type-Safe React to C# Web Message Bridge

            Inside WebView2, user interactions in React post structured JSON messages to the C# WPF host.

            ### 1. TypeScript Contract
            ```typescript
            export interface HostBridgeAction<T = unknown> {
                type: 'INSERT_SHAPE' | 'GENERATE_AI_DIAGRAM' | 'EXPORT_DATA';
                payload: T;
            }

            export function sendToHost<T>(action: HostBridgeAction<T>): void {
                if (window.chrome?.webview) {
                    window.chrome.webview.postMessage(action);
                }
            }
            ```

            ### 2. C# Dispatcher & STA Threading Protection
            Because PowerPoint's COM object model requires calls to originate on a Single-Threaded Apartment (STA) thread, asynchronous operations in .NET 9 must never directly call into `PowerPoint.Application` from thread-pool threads:

            ```csharp
            public class AddInBridge
            {
                private readonly Microsoft.Office.Interop.PowerPoint.Application _powerPoint;
                private readonly SynchronizationContext _staContext;

                public AddInBridge(Microsoft.Office.Interop.PowerPoint.Application app)
                {
                    _powerPoint = app;
                    _staContext = SynchronizationContext.Current ?? new SynchronizationContext();
                }

                public void HandleMessage(string json)
                {
                    using var doc = JsonDocument.Parse(json);
                    var type = doc.RootElement.GetProperty("type").GetString();

                    if (type == "INSERT_SHAPE")
                    {
                        var text = doc.RootElement.GetProperty("payload").GetProperty("text").GetString();

                        // Safely marshal into PowerPoint's STA thread
                        _staContext.Post(_ =>
                        {
                            var slide = _powerPoint.ActiveWindow.View.Slide;
                            var shape = slide.Shapes.AddShape(
                                MsoAutoShapeType.msoShapeRoundedRectangle, 100, 100, 200, 80);
                            shape.TextFrame.TextRange.Text = text;
                        }, null);
                    }
                }
            }
            ```

            ## Seamless Shared Component Libraries

            The crowning benefit of this architecture is UI code reuse. The exact same React timeline visualizer, theme switcher, and AI prompt controls running inside PowerPoint's native taskpane can be shared with web SaaS applications and standalone browser tools without rewriting a single component.

            Learn more about the full desktop topology this pattern anchors:
            [Enterprise .NET 9 Desktop Architecture & AI Agent Tooling](/article/enterprise-dotnet-9-desktop-architecture-ai-tooling).
            """",
    };

    public static readonly IReadOnlyList<Article> All = [Pillar, ClusterIpc, ClusterUnity, ClusterOffice];

    public static Article? FindBySlug(string slug)
        => All.FirstOrDefault(a => a.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
}
