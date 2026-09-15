namespace AtomicArt.Desktop.Services.Dlss5;

public static class Dlss5FeatureDefinition
{
    public const string ModuleDirectoryName = "DLSS 5";
    public const string Version = "7.0";
    public const long DownloadSizeBytes = 166_588_922;
    public const string DownloadSizeDisplay = "158.87 MiB";
    public const string DownloadUrl =
        "https://github.com/Krivodel/AtomicArt-Modules/releases/download/1.0.0/DLSS.5.zip";
    public const string DownloadSha256 =
        "5294A4A6D2C0B5D57E94B868CEBCFB62F56E2D56D6461CBEFCAC2D32E532A47B";
    public const int MaxSourceImageBytes = 128 * 1024 * 1024;
    public const string NvidiaIconGeometry =
        "F1 M82.211 102.414s22.504-33.203 67.437-36.638V53.73c-49.769 3.997-92.867 46.149-92.867 46.149s24.41 70.565 92.867 77.026v-12.804c-50.237-6.32-67.437-61.687-67.437-61.687zm67.437 36.223v11.726c-37.968-6.769-48.507-46.237-48.507-46.237s18.23-20.195 48.507-23.47v12.867c-.023 0-.039-.007-.058-.007-15.891-1.907-28.305 12.938-28.305 12.938s6.958 24.991 28.363 32.183m0-107.125V53.73c1.461-.112 2.922-.207 4.391-.257 56.582-1.907 93.449 46.406 93.449 46.406s-42.343 51.488-86.457 51.488c-4.043 0-7.828-.375-11.383-1.005v13.739c3.04.386 6.192.613 9.481.613 41.051 0 70.738-20.965 99.484-45.778 4.766 3.817 24.278 13.103 28.289 17.168-27.332 22.883-91.031 41.329-127.144 41.329-3.481 0-6.824-.211-10.11-.528v19.306H305.68V31.512H149.648zm0 49.144V65.777c1.446-.101 2.903-.179 4.391-.226 40.688-1.278 67.382 34.965 67.382 34.965s-28.832 40.043-59.746 40.043c-4.449 0-8.438-.715-12.028-1.922V93.523c15.84 1.914 19.028 8.911 28.551 24.786l21.18-17.859s-15.461-20.277-41.524-20.277c-2.833-.001-5.544.198-8.206.483";

    public static readonly IReadOnlyList<Dlss5RuntimeFile> RuntimeFiles =
        new List<Dlss5RuntimeFile>
    {
        new Dlss5RuntimeFile(
            "bin/runtime/host/nvngx.dll",
            "host/nvngx.dll",
            82_432,
            "666113F7EC4E987AA64E1389656949864AD9C0DC1C233560DF41A5C01F68BBB9"),
        new Dlss5RuntimeFile(
            "bin/runtime/host/dxgi.dll",
            "host/dxgi.dll",
            5_592_064,
            "0CEE63F9C9F13F3AC909C5B4903F4DBB4B719A7AB3B4F13B0DEAF83C814B94F7"),
        new Dlss5RuntimeFile(
            "bin/runtime/dlssnr/nvngx_dlssnr.dll",
            "dlssnr/nvngx_dlssnr.dll",
            165_830_144,
            "6EB209E764F39872625DEBD6ABAF45E2BB6322F6F270F781F70C059AE30B3927"),
        new Dlss5RuntimeFile(
            "bin/runtime/dlss/nvngx_dlss.dll",
            "dlss/nvngx_dlss.dll",
            74_187_376,
            "07C7FEA19A24C75102BF34D1A4A775640CE323A0B2265D4887B331E62486C44F"),
        new Dlss5RuntimeFile(
            "bin/runtime/dlssnr/renodx-dlss5.addon64",
            "dlssnr/renodx-dlss5.addon64",
            1_732_608,
            "D5ADF82EB44B065F4C590AC91FE824BAB07AFEA0EB9F994BDE936710C8593952"),
        new Dlss5RuntimeFile(
            "bin/runtime/host/LICENSE-NVIDIA-DLSS.txt",
            "host/LICENSE-NVIDIA-DLSS.txt",
            27_176,
            "21B5DAEC892B12BEA692E66BC8FE45CF5902CCAF3A7B831E78050D8859881C37"),
        new Dlss5RuntimeFile(
            "bin/runtime/dlssnr/LICENSE-RenoDX.txt",
            "dlssnr/LICENSE-RenoDX.txt",
            1_410,
            "BEA728F0DF1E6C4D69E1B284AB79AFE709F049B082B6A1322A6F9C53F0C73DB0"),
        new Dlss5RuntimeFile(
            "bin/runtime/dlssnr/dlss5-event-dynamic.addon64",
            "dlssnr/dlss5-event-dynamic.addon64",
            288_256,
            "0BB03EE5BE88F97BB6E9BA664258747F0ED894945EF6BCA49E955AB66B43E214"),
        new Dlss5RuntimeFile(
            "bin/runtime/host/LICENSE-ReShade.txt",
            "host/LICENSE-ReShade.txt",
            1_871,
            "72CBE607344D850C849ED42AF0676217B25CE95A8BC5355C62F5AFB5C34F2992",
            true),
        new Dlss5RuntimeFile(
            "bin/runtime/dlss/LICENSE-NVIDIA-DLSS.txt",
            "dlss/LICENSE-NVIDIA-DLSS.txt",
            27_176,
            "21B5DAEC892B12BEA692E66BC8FE45CF5902CCAF3A7B831E78050D8859881C37",
            true)
    };
}
