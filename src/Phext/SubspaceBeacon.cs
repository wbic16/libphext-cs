// ===========================================================================================================
// libphext-cs: The Fourth Root
// SubspaceBeacon.cs - Helper structure for subspace navigation
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

namespace Phext;

/// <summary>
/// Represents the start/end offsets and best coordinate match found during subspace traversal
/// </summary>
public class SubspaceBeacon
{
    public int Start { get; set; }
    public int End { get; set; }
    public Coordinate Best { get; set; }

    public SubspaceBeacon()
    {
        Start = 0;
        End = 0;
        Best = new Coordinate();
    }

    public SubspaceBeacon(int start, int end, Coordinate best)
    {
        Start = start;
        End = end;
        Best = new Coordinate(best);
    }
}
