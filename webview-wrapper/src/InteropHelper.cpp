#include "InteropHelper.h"
#include <algorithm>
#include <format>
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
    DWORD size = MAX_PATH;
    std::vector<wchar_t> buf(size);
    DWORD len;

    while ((len = GetModuleFileNameW(NULL, buf.data(), size)) == size) {
        size *= 2;
        buf.resize(size);
    }

    if (len == 0) return L"WebUI\\index.html";

    std::wstring fullPath(buf.data(), len);
    auto last_slash = fullPath.find_last_of(L"\\/");
    if (last_slash != std::wstring::npos) {
        return fullPath.substr(0, last_slash) + L"\\WebUI\\index.html";
    }
    return L"WebUI\\index.html";
}

std::string InteropHelper::JsonEscape(std::string_view s) {
    std::string result;
    result.reserve(s.size() + 8);

    for (char c : s) {
        switch (c) {
        case '"':  result += "\\\""; break;
        case '\\': result += "\\\\"; break;
        case '\n': result += "\\n";  break;
        case '\r': result += "\\r";  break;
        case '\t': result += "\\t";  break;
        default:
            if (static_cast<unsigned char>(c) < 0x20) {
                result += std::format("\\u{:04X}", static_cast<unsigned char>(c));
            } else {
                result += c;
            }
            break;
        }
    }

    return result;
}

} // namespace Immersive
