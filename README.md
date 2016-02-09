# GraveRobber [![Build status](https://ci.appveyor.com/api/projects/status/mvxu2d9jk42ypvlk/branch/master?svg=true)](https://ci.appveyor.com/project/ArcticEcho/graverobber/branch/master)

GraveRobber is a small project which aims to help the SOCVR in finding potentially reopen-worthy questions which have been closed via a `[cv-pls]` request. The bot will post a message in a chatroom once a `[cv-pls]`'ed question has been edited above a specified "change %", that is, if a question is *x*% different to when it was closed, it will be reported.

Example report: 

> 74% changed: [question](http://stackoverflow.com/q/35267717) (+1/-8) - [req](http://chat.stackoverflow.com/transcript/message/28628733)

-----

For instructions on running this program in docker, refer to the comments in the [docker-compose.yml.example](/docker-compose.yml.example) file.
