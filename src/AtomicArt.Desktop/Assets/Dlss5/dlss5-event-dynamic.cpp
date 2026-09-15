#define WIN32_LEAN_AND_MEAN
#define NOMINMAX

#include <windows.h>
#include <psapi.h>

#include <cstdio>
#include <fstream>

#pragma comment(lib, "psapi.lib")

using RegisterAddon = bool (*)(HMODULE, unsigned int);
using RegisterEvent = void (*)(unsigned int, void *);

static HMODULE g_renodx = nullptr;
static bool g_logged = false;

static void log_line(const char *text)
{
    FILE *file = nullptr;
    fopen_s(&file, "dlss5-event-dynamic.log", "a");
    if (file != nullptr)
    {
        fprintf(file, "%s\n", text);
        fclose(file);
    }
}

static HMODULE find_renodx()
{
    if (g_renodx != nullptr)
    {
        return g_renodx;
    }

    g_renodx = GetModuleHandleA("renodx-dlss5.addon64");
    return g_renodx;
}

static void apply_parameters()
{
    HMODULE module = find_renodx();
    if (module == nullptr)
    {
        return;
    }

    std::ifstream input("dlss5-event-dynamic.params");
    int style = 2;
    float intensity = 0.0f;
    float globalTone = 1.0f;
    float localTone = 0.0f;
    float localStructure = 0.0f;
    float skin = 0.0f;
    if (!(input >> style >> intensity >> globalTone >> localTone >> localStructure >> skin)
        || style < 0 || style > 2
        || intensity < 0.0f || intensity > 2.0f
        || globalTone < 0.0f || globalTone > 2.0f
        || localTone < 0.0f || localTone > 2.0f
        || localStructure < 0.0f || localStructure > 2.0f
        || skin < -1.0f || skin > 2.0f)
    {
        return;
    }

    unsigned char *base = reinterpret_cast<unsigned char *>(module);
    *reinterpret_cast<int *>(base + 0x196c2c) = style;
    *reinterpret_cast<float *>(base + 0x19364c) = intensity;
    *reinterpret_cast<float *>(base + 0x193650) = globalTone;
    *reinterpret_cast<float *>(base + 0x193654) = localTone;
    *reinterpret_cast<float *>(base + 0x193658) = localStructure;
    *reinterpret_cast<float *>(base + 0x19365c) = skin;
    if (!g_logged)
    {
        log_line("dynamic parameter patch active");
        g_logged = true;
    }
}

static void on_init(void *)
{
    apply_parameters();
}

static void on_execute_command_list(void *, void *)
{
    apply_parameters();
}

static BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason != DLL_PROCESS_ATTACH)
    {
        return TRUE;
    }

    DisableThreadLibraryCalls(module);
    HMODULE modules[128]{};
    DWORD moduleBytes = 0;
    if (!K32EnumProcessModules(GetCurrentProcess(), modules, sizeof(modules), &moduleBytes))
    {
        return FALSE;
    }

    for (unsigned int index = 0; index < moduleBytes / sizeof(HMODULE); ++index)
    {
        RegisterAddon registerAddon = reinterpret_cast<RegisterAddon>(
            GetProcAddress(modules[index], "ReShadeRegisterAddon"));
        if (registerAddon == nullptr || !registerAddon(module, 18))
        {
            continue;
        }

        RegisterEvent registerEvent = reinterpret_cast<RegisterEvent>(
            GetProcAddress(modules[index], "ReShadeRegisterEvent"));
        if (registerEvent != nullptr)
        {
            registerEvent(9, reinterpret_cast<void *>(&on_init));
            registerEvent(72, reinterpret_cast<void *>(&on_execute_command_list));
        }
        return TRUE;
    }

    return FALSE;
}
