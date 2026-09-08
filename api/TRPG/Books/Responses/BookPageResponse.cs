namespace TRPG.Books.Responses;

public record BookPageResponse(
    string Title,
    int PageNumber,
    int PageCount,
    string Text,
    bool RevealedSecret
);
