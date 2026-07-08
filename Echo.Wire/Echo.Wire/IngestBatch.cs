using System.Collections.Generic;

namespace Echo.Wire;

public record IngestBatch(int ProtocolVersion, string PluginVersion, ReporterSelf Reporter, List<Sighting> Sightings);
