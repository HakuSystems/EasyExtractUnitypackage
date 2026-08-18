namespace EasyExtract.Core.Models;

/// <summary>
///     Why an extraction wrote the number of assets it wrote. An asset count of zero is not an
///     error by itself: a caller-supplied filter or an output folder that is already up to date
///     both produce zero legitimately. Only <see cref="NoAssetsRecognized" /> means the package
///     was not understood.
/// </summary>
public enum UnityPackageExtractionOutcome
{
    /// <summary>At least one asset was written to disk.</summary>
    Extracted,

    /// <summary>Nothing was written and there is a known reason for it.</summary>
    NothingToDo,

    /// <summary>
    ///     Nothing was written and nothing explains it. The package had entries but none of them
    ///     matched Unity's asset layout, so the run looked successful while leaving an empty folder.
    /// </summary>
    NoAssetsRecognized
}
