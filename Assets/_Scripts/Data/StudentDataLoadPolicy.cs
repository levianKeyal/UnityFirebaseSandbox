public enum StudentDataLoadPolicy
{
    // Required during initial user restoration before gameplay state is considered ready.
    RestoreOnLogin,

    // Part of normal user loading, but not a critical prerequisite for starting gameplay.
    LoadNormally,

    // Deferred until a later explicit request; no initial load is implied.
    LoadOnDemand
}
