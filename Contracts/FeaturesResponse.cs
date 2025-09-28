public record FeatureDto(
    string Key,
    string Name,
    bool Premium,
    bool RequiresFamily,
    bool AssignedToUser,
    bool Enabled,
    int Order,
    string Icon,
    string Route
);

public record FeaturesResponse(
    List<FeatureDto> FeatureList,
    int Code,
    string Description,
    Guid RequestId
);
