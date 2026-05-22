#pragma once
#include <windows.h>
#include <string>
#include <concepts>

namespace Immersive {

// C++20 Concept: Validates that a type is a Win32 Handle
template<typename T>
concept Win32Handle = std::is_pointer_v<T> || std::is_same_v<T, HWND> || std::is_same_v<T, HANDLE>;

class InteropHelper {
public:
    // String Conversion: UTF8 to Wide (Using modern char8_t context if needed)
    static std::wstring Utf8ToWide(const char* utf8);
    
    // String Conversion: Wide to UTF8
    static std::string WideToUtf8(LPCWSTR wide);
    
    // Convert local file path to file:/// URI
    static std::wstring PathToUri(std::wstring_view path);
    
    // Get absolute path to index.html
    static std::wstring GetWebUiPath();

    // JSON string escaping (escapes ", \, \n, \r, \t, and control chars)
    static std::string JsonEscape(std::string_view s);

    // C++20 Template with Concept
    template<Win32Handle T>
    static bool IsValid(T handle) {
        return handle != nullptr && handle != INVALID_HANDLE_VALUE;
    }
};

} // namespace Immersive
