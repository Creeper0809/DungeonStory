public static class CodexEvolutionRecorder
{
    public static void Record(CodexState state, FacilityEvolutionResult result)
    {
        if (state == null || !result.Success || result.Recipe == null)
        {
            return;
        }

        BuildingSO resultBuilding = result.ResultBuilding != null
            ? result.ResultBuilding.BuildingData
            : result.Recipe.resultBuilding;
        if (resultBuilding == null)
        {
            return;
        }

        string resultName = FacilityShopService.GetBuildingName(resultBuilding);
        string sourceName = !string.IsNullOrWhiteSpace(result.SourceFacilityName)
            ? result.SourceFacilityName
            : "이전 시설";

        CodexObservationRecorder.ObserveFacility(state, resultBuilding, CodexInfoSource.Evolution);
        CodexFacilityInfoWriter.Add(
            state,
            resultBuilding,
            $"계보 진화: {sourceName} -> {resultName} ({result.ResultStarGrade}성)",
            CodexInfoSource.Evolution);
        CodexFacilityInfoWriter.Add(
            state,
            resultBuilding,
            $"진화식: {result.Recipe.DisplayName}",
            CodexInfoSource.Evolution);

        FacilityEvolutionProposal proposal = result.Proposal;
        AddLineIfNotBlank(state, resultBuilding, "정체성", proposal.FacilityIdentitySummary);
        AddLineIfNotBlank(state, resultBuilding, "진화 기록", proposal.FlavorText);
        AddLineIfNotBlank(state, resultBuilding, "해석 출처", proposal.Source);

        string mutationText = CodexTextFormatter.FormatEvolutionMutationTags(result.MutationTags);
        AddLineIfNotBlank(state, resultBuilding, "변이", mutationText);
    }

    private static void AddLineIfNotBlank(
        CodexState state,
        BuildingSO building,
        string label,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        CodexFacilityInfoWriter.Add(state, building, $"{label}: {value}", CodexInfoSource.Evolution);
    }
}
