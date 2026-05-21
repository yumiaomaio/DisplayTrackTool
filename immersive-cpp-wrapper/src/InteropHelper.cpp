#include "InteropHelper.h"
#include <algorithm>
#include <vector>

namespace Immersive {

std::wstring InteropHelper::Utf8ToWide(const char* utf8) {
    if (!utf8 || utf8[0] == '\0') return L"";
    int size_needed = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, NULL, 0);
    if (size_needed <= 1) return L"";
    
    std::vector<wchar_t> buffer(size_needed);
    MultiByteToWideChar(CP_UTF8, 0, utf8, -1, buffer.data(), size_needed);
    
    // Explicitly exclude the null terminator from the resulting wstring
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

std::wstring InteropHelper::PathToUri(std::wstring path) {
    std::replace(path.begin(), path.end(), L'\\', L'/');
    return L"file:///" + path;
}

std::wstring InteropHelper::GetWebUiPath() {
    wchar_t path[MAX_PATH];
    GetModuleFileNameW(NULL, path, MAX_PATH);
    std::wstring fullPath(path);
    size_t last_slash = fullPath.find_last_of(L"\\/");
    if (last_slash != std::wstring::npos) {
        return fullPath.substr(0, last_slash) + L"\\WebUI\\index.html";
    }
    return L"WebUI\\index.html";
}

} // namespace Immersive
