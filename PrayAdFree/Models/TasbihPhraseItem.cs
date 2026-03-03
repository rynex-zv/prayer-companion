namespace Pray_Ad_Free.Models;

public sealed class TasbihPhraseItem {
    public TasbihPhraseItem(string title, string body) {
        Title = title;
        Body = body;
    }

    public string Title { get; }
    public string Body { get; }
}
