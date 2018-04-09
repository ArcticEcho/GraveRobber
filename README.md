# GraveRobber

GraveRobber is a small project which aims to help the SOCVR in monitoring questions that, via a `[cv-pls]` request, have been closed or are still pending closure. The bot will post a message in the chatroom once a question has been edited above a specified "change %", that is, if a question is *x*% different to when it was closed, it will be reported.

[Example report](https://chat.stackoverflow.com/transcript/41570?m=42016101#42016101): 

> [62%](https://stackoverflow.com/posts/49716181/revisions "Adjusted: 62%. Distance: 665.") changed, +61% code, -100% formatting (by OP): [question](https://stackoverflow.com/q/49716181) (+1/-5)  - [req](https://chat.stackoverflow.com/transcript/message/42005792) @AdrianHHH


# What do the numbers mean?

GraveRobber compares the latest state (revision) of the question to when the revision at the time of when the `[cv-pls]` request was issued for it. The numbers indicate various aspects of the changes made between those revisions.

Using the above example report:

 - `62% changed`: This is the overall change metric, how much rendered text has been modified. This number can only be positive.
 - <sup>1</sup> `Adjusted: 6%`: Same as above, but returns a lower score for smaller posts below a set threshold. This number is used to determine whether a report should be posted.
 - <sup>1</sup> `Distance: 665`: This is the raw edit distance returned from the Damerau-Levenshtein Distance function.
 - `+61% code`: This measures how much code has been added/removed when compared to the older revision. This number can be positive or negative. In this case, *+61%* indicates that the post now contains 61% more code.
 - `-100% formatting`: Similar to above, but used to measure formatted text (excluding code blocks). This example shows that the question now has no formatted text.
 - `(+1/-5)`: Is simply a break-down of the question's up- and down-votes.
 
 <sup>1</sup> Can only be seen by hovering over the first link in the report.
