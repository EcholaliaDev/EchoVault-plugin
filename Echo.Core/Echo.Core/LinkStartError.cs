namespace Echo.Core;

public record LinkStartError(string Error, int? AgeDays = null, int? AgeRequiredDays = null, int? Observed = null, int? ObservedRequired = null);
