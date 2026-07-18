namespace AtomicArt.Contracts.Generation;

public static class GenerationImageFileFormats
{
    public static IReadOnlyList<GenerationImageFileFormatDescriptor> All { get; } =
    [
        new(
                GenerationImageContentTypes.Gif,
                [".gif"],
                [
                    [
                        new(0, GenerationImageFileSignatures.Gif87A.ToArray())
                    ],
                    [
                        new(0, GenerationImageFileSignatures.Gif89A.ToArray())
                    ]
                ]),
            new(
                GenerationImageContentTypes.Heic,
                [".heic"],
                [
                    [
                        new(4, GenerationImageFileSignatures.Ftyp.ToArray()),
                        new(
                            GenerationImageFileSignatures.HeifBrandOffset,
                            GenerationImageFileSignatures.HeicBrand.ToArray())
                    ],
                    [
                        new(4, GenerationImageFileSignatures.Ftyp.ToArray()),
                        new(
                            GenerationImageFileSignatures.HeifBrandOffset,
                            GenerationImageFileSignatures.HeixBrand.ToArray())
                    ]
                ]),
            new(
                GenerationImageContentTypes.Heif,
                [".heif"],
                [
                    [
                        new(4, GenerationImageFileSignatures.Ftyp.ToArray()),
                        new(
                            GenerationImageFileSignatures.HeifBrandOffset,
                            GenerationImageFileSignatures.Mif1Brand.ToArray())
                    ],
                    [
                        new(4, GenerationImageFileSignatures.Ftyp.ToArray()),
                        new(
                            GenerationImageFileSignatures.HeifBrandOffset,
                            GenerationImageFileSignatures.Msf1Brand.ToArray())
                    ]
                ]),
            new(
                GenerationImageContentTypes.Jpeg,
                [".jpg", ".jpeg"],
                [
                    [
                        new(0, GenerationImageFileSignatures.Jpeg.ToArray())
                    ]
                ]),
            new(
                GenerationImageContentTypes.Png,
                [".png"],
                [
                    [
                        new(0, GenerationImageFileSignatures.Png.ToArray())
                    ]
                ]),
            new(
                GenerationImageContentTypes.Webp,
                [".webp"],
                [
                    [
                        new(0, GenerationImageFileSignatures.Riff.ToArray()),
                        new(
                            GenerationImageFileSignatures.WebpFormatOffset,
                            GenerationImageFileSignatures.Webp.ToArray())
                    ]
                ])
    ];
}
