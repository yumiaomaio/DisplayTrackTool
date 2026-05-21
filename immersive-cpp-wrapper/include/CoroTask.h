#pragma once
#include <coroutine>
#include <exception>
#include <windows.h>

namespace Immersive {

// 1. Task: For awaited coroutines
template<typename T = HRESULT>
struct Task {
    struct promise_type {
        T result = S_OK;
        std::exception_ptr exception;

        Task get_return_object() { return Task(std::coroutine_handle<promise_type>::from_promise(*this)); }
        std::suspend_never initial_suspend() { return {}; }
        std::suspend_always final_suspend() noexcept { return {}; }
        void return_value(T v) { result = v; }
        void unhandled_exception() { exception = std::current_exception(); }
    };

    std::coroutine_handle<promise_type> handle;
    Task(std::coroutine_handle<promise_type> h) : handle(h) {}
    Task(Task&& other) noexcept : handle(other.handle) { other.handle = nullptr; }
    
    // IMPORTANT: Only destroy if we are not moving or if it's a specific managed task.
    // For InitializeAsync, we'll use a different type to avoid premature destruction.
    ~Task() { if (handle) handle.destroy(); }
};

// 2. AsyncVoid: Fire-and-forget task that cleans up itself
struct AsyncVoid {
    struct promise_type {
        AsyncVoid get_return_object() { return {}; }
        std::suspend_never initial_suspend() { return {}; }
        std::suspend_never final_suspend() noexcept { return {}; } // AUTO-CLEANUP
        void return_void() {}
        void unhandled_exception() { /* Log or terminate */ }
    };
};

} // namespace Immersive
