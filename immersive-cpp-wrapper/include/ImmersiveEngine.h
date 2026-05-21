#pragma once
#include <windows.h>

// Define the function pointers for the C-compatible exports in the DLL
extern "C" {
    // Creates a new instance of the ImmersiveEngine and returns its handle.
    __declspec(dllimport) void* immersive_create();
    
    // Initializes the engine and sets up a callback for state updates.
    // callback is a function pointer: void (const char* json)
    __declspec(dllimport) void immersive_initialize(void* handle, void (__stdcall *callback)(const char*));
    
    // Sets the protocol auto-start flag.
    __declspec(dllimport) void immersive_set_protocol_autostart(void* handle, int isAutoStart);
    
    // Sends a JSON message to the engine. Returns a pointer to the result JSON string.
    // The caller MUST call immersive_free_string on the returned pointer.
    __declspec(dllimport) const char* immersive_handle_message(void* handle, const char* jsonPtr);
    
    // Frees a string allocated by the DLL.
    __declspec(dllimport) void immersive_free_string(const char* ptr);
    
    // Disposes the engine and releases the handle.
    __declspec(dllimport) void immersive_dispose(void* handle);
}
