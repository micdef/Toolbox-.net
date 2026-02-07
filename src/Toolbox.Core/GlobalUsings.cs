// @file GlobalUsings.cs
// @brief Global using directives for Toolbox.Core
// @details Centralizes common namespace imports used throughout the library
// @note These global usings are automatically available in all source files within
//       the Toolbox.Core assembly, reducing boilerplate imports and ensuring
//       consistent namespace usage across the codebase.
//
// @section namespaces Included Namespaces
// - System.Diagnostics: Performance monitoring and tracing
// - System.Diagnostics.Metrics: OpenTelemetry-compatible metrics API
// - System.Runtime.CompilerServices: Compiler services and attributes
// - Microsoft.Extensions.*: Dependency injection, logging, and options patterns
// - OpenTelemetry.*: Distributed tracing and metrics collection

global using System.Diagnostics;
global using System.Diagnostics.Metrics;
global using System.Runtime.CompilerServices;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using OpenTelemetry;
global using OpenTelemetry.Metrics;
global using OpenTelemetry.Trace;
