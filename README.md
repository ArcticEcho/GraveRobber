# GraveRobber

GraveRobber is a small project which aims to help the SOCVR in monitoring questions that, via a `[cv-pls]` request, have been closed or are still pending closure. The bot will post a message in the chatroom once a question has been edited above a specified "change %", that is, if a question is *x*% different to when it was closed, it will be reported.

Example report: 

> [ [GraveRobber](https://github.com/SO-Close-Vote-Reviewers/GraveRobber) ] [45%](https://stackoverflow.com/posts/2147483647/revisions "Adjusted: 42%. Distance: 345.") changed (by OP), affecting code by 84% and formatting by 6%: [question](https://stackoverflow.com/q/2147483647) (-4/+1) - [req](https://chat.stackoverflow.com/transcript/message/2147483647) @​Username


# What do the numbers mean?

GraveRobber compares the latest state (revision) of the question to when the revision at the time of when the `[cv-pls]` request was issued for it. The numbers indicate various aspects of the changes made between those revisions.

Using the above example report:

 - `45% changed`: This is the overall change metric, how much rendered text has been modified.
 - <sup>1</sup> `Adjusted: 42%`: Same as above, but returns a lower score for smaller posts below a set threshold. This number is used to determine whether a report should be posted.
 - <sup>1</sup> `Distance: 345`: This is the raw edit distance returned from the Damerau-Levenshtein Distance function.
 - `code by 84%`: This measures how much of the edit affects code. In this case, 84% of the total characters changed by this edit were code characters.
 - `formatting by 6%`: Similar to above, but used to measure formatted text (excluding code blocks). This example shows that 6% of the total edited characters affected formatted text.
 - `(-4/+1)`: Is simply a break-down of the question's up- and down-votes.
 
 <sup>1</sup> Can only be seen by hovering over the first link in the report.
