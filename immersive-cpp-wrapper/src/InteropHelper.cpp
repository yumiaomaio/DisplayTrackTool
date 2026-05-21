#include "InteropHelper.h"
#include <algorithm>
#include <vector>
#include <ranges>

namespace Immersive {

std::wstring InteropHelper::Utf8ToWide(const char* utf8) {
    if (!utf8 || utf8[0] == '\0') return L"";
    int size_needed = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, NULL, 0);
    if (size_needed <= 1) return L"";
    
    std::vector<wchar_t> buffer(size_needed);
    MultiByteToWideChar(CP_UTF8, 0, utf8, -1, buffer.data(), size_needed);
    
    return std::wstring(buffer.data(), size_needed - 1);
}

std::string InteropHelper::WideToUtf8(LPCWSTR wide) {
    if (!wide || wide[0] == L'\0') return "";
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, wide, -1, NULL, 0, NULL, NULL);
    if (size_needed <= 1) return "";
    
    std::vector<char> buffer(size_needed);
    WideCharToMultiByte(CP_UTF8, 0, wide, -1, buffer.data(), size_needed, NULL, NULL);
    
    return std::string(buffer.data(), size_needed - 1);
}

std::wstring InteropHelper::PathToUri(std::wstring_view path) {
    // C++20/23: Use string_view and dynamic replacement
    std::wstring result{path};
    // C++20 Ranges example (even if simple replace is easier)
    std::ranges::replace(result, L'\\', L'/');
    return L"file:///" + result;
}

std::wstring InteropHelper::GetWebUiPath() {
    wchar_t path[MAX_PATH];
    GetModuleFileNameW(NULL, path, MAX_PATH);
    std::wstring fullPath(path);
    
    // C++20: string_view based search
    std::wstring_view sv(fullPath);
    if (auto last_slash = sv.find_last_of(L"\\/"); last_slash != std::wstring_view::npos) {
        return std::wstring(sv.substr(0, last_slash)) + L"\\WebUI\\index.html";
    }
    return L"WebUI\\index.html";
}

} // namespace Immersive
