﻿using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ProjectService.Infrastructure.Converters;

public class UlidToBytesConverter : ValueConverter<Ulid, byte[]>
{
    private static readonly ConverterMappingHints DefaultHints = new(size: 16);

    public UlidToBytesConverter() : this(null)
    {
    }
    
    public UlidToBytesConverter(ConverterMappingHints? mappingHints)
        : base(
            convertToProviderExpression: x => x.ToByteArray(),
            convertFromProviderExpression: x => new Ulid(x),
            mappingHints: DefaultHints.With(mappingHints))
    {
    }
}