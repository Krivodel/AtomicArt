# DLSS 5 source preparation

Run on Windows with a supported NVIDIA GPU and the installed DLSS 5 module:

```powershell
dotnet run --project benchmarks/AtomicArt.Dlss5.Benchmarks -c Release -- '<Atomic Art data root>' '<source image>'
```

Use an image at least 128×128, with dimensions other than 512×512. Close other GPU workloads when comparing timings. The benchmark creates its own engine and isolated workers, reads the supplied image without modifying it, and stops its workers on completion. It does not run the module installer.

The benchmark measures preparation and time to the first result for two image sizes, then returns to the first size with different settings. Exactly two workers must be started before the reference run. Returning to the first size must reuse its worker even though its settings changed.

It also compares the output bytes against a fresh worker explicitly warmed with an opaque black frame, matching the previous preparation path. Both the first result and the result after changing settings must match. A failed worker-count or pixel comparison exits with code 1.

Keep `total_ms` alongside `prepare_ms`: native feature initialization now happens on the first real frame, so preparation time alone does not measure time to a usable result. A previously unseen size still needs native ReShade/NGX initialization; GPU load and driver initialization affect that duration.
