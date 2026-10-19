using OpenWinter.Client.UI;
using Xunit;

namespace OpenWinter.Tests;

public class QuickbarSpellCountsTests
{
    [Fact]
    public void EmptyLoadedBookAndMissingPreparationShowZero()
    {
        var loading = QuickbarSpellCounts.Read([], false, 46, 2);
        Assert.Equal(-1, loading.Badge);
        Assert.True(loading.Usable);
        foreach (var entries in new BookSpell[][] { [], [new(6, 2, 1, 0, 1, 1)] })
        {
            var empty = QuickbarSpellCounts.Read(entries, true, 46, 2);
            Assert.Equal(0, empty.Badge);
            Assert.False(empty.Usable);
        }
    }

    [Fact]
    public void ClassAndMetamagicDoNotBorrowOtherPreparedCopies()
    {
        BookSpell[] entries = [new(6, 2, 1, 0, 0, 2), new(6, 2, 2, 2, 1, 1), new(6, 10, 1, 0, 3, 3)];
        Assert.Equal(new QuickbarSpellCounts(0, 2, true), QuickbarSpellCounts.Read(entries, true, 6, 2, 0));
        Assert.Equal(1, QuickbarSpellCounts.Read(entries, true, 6, 2, 255).Badge);
        Assert.Equal(0, QuickbarSpellCounts.Read(entries, true, 6, 2, 8).Badge);
        Assert.Equal(4, QuickbarSpellCounts.Read(entries, true, 6, -1, 255).Badge);
    }

    [Fact]
    public void SpontaneousPoolAndInnateCopiesRemainSeparate()
    {
        BookSpell[] entries = [new(107, 9, 1, 0, 2, 5), new(102, 9, 1, 0, 2, 5), new(107, 255, -1, 0, 1, 2)];
        Assert.Equal(new QuickbarSpellCounts(2, 5, true), QuickbarSpellCounts.Read(entries, true, 107, 9));
        Assert.Equal(new QuickbarSpellCounts(1, 2, true), QuickbarSpellCounts.Read(entries, true, 107, 255));
        Assert.False(QuickbarSpellCounts.Read(entries, true, 107, 2).Usable);
    }
}
