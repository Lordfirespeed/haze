#!/usr/bin/env node

const foo = new WebSocket("ws://localhost:5000/api/websocket")

function afterOpen() {
  console.log("heck yeah we open")
  foo.send(JSON.stringify({"$type": "new-session-v1"}))
  foo.send(JSON.stringify({"$type": "authenticate-v1", "client_id": "fancy-client", "client_secret": "a-very-fancy-secret"}))
}

foo.addEventListener("message", function(event) {
  console.log(`message: ${event.data}`);
})
foo.addEventListener("close", function(event) {
  console.log(`close: ${event.code}, ${event.reason}`)
})
foo.addEventListener("error", function(event) {
  console.log(`an error occurred`)
})

foo.addEventListener("open", (event) => afterOpen());
if (foo.readyState === foo.OPEN) afterOpen();
