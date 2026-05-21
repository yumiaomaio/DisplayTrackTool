#pragma once
#include <windows.h>
#include <string>

namespace Immersive {

class InteropHelper {
public:
    // String Conversion: UTF8 to Wide
    static std::wstring Utf8ToWide(const char* utf8);
    
    // String Conversion: Wide to UTF8
    static std::string WideToUtf8(LPCWSTR wide);
    
    // Convert local file path to file:/// URI
    static std::wstring PathToUri(std::wstring path);
    
    // Get absolute path to index.html
    static std::wstring GetWebUiPath();
};

} // namespace Immersive
