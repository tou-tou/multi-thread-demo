# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a C# .NET 8.0 console application that demonstrates multithreading concepts, specifically comparing different approaches to implementing thread-safe method call counters. The project is designed as an educational lightning talk demonstration showing the trade-offs between performance and correctness in concurrent programming.

## Build and Development Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Clean build artifacts
dotnet clean

# Publish the application
dotnet publish
```

## Architecture and Key Components

### Core Interface
All counter implementations follow the `IMethodCounter` interface defined in `MethodCounter.cs`:
- `void Record(string methodName)` - Records a method call
- `Dictionary<string, int> GetCountsAndReset()` - Retrieves counts and resets the counter

### Three Implementation Strategies

1. **MethodCounter_NotThreadSafe** - Naive implementation using Dictionary
   - Demonstrates thread-safety failures
   - Causes exceptions and data corruption under concurrent access

2. **MethodCounter_WithLock** - Thread-safe implementation using lock statements
   - Guarantees correctness but has performance overhead
   - Uses dedicated lock object for synchronization

3. **MethodCounter_LockFree** - High-performance lock-free implementation
   - Uses ConcurrentQueue and Interlocked.Exchange
   - Better performance but can lose data during queue swapping

### Program Flow
The `Program.cs` demonstrates these implementations through 4 steps:
1. Shows thread-unsafe implementation failures
2. Proves lock-based solution correctness
3. Compares performance between implementations
4. Reveals data loss issues in lock-free approach

### Testing Parameters
- ThreadCount: 100 concurrent threads
- CallsPerThread: 10,000 operations per thread
- TotalCalls: 1,000,000 expected operations

## Important Design Considerations

- The lock-free implementation trades a small amount of data loss for significantly better performance
- Performance tests use 100 different method names to simulate realistic load distribution
- Each test explicitly calls GC.Collect() before execution to ensure consistent conditions
- The project includes Japanese documentation in `docs/` explaining the concepts for presentations