using DungeonStory.Foundation.Facts;


// The source-facing contract contains only domain publication and its save fence.
public interface ISocialRitualFactPublisher : IDomainFactPublisher
{
    void AssertIdle();
}
