#!/usr/bin/env node

const foo = new WebSocket("ws://localhost:5000/api/websocket")

function afterOpen() {
  console.log("heck yeah we open")
  foo.send(JSON.stringify({"$type": "authenticate-v1", "client_id": "foo", "client_secret": "bar"}))
}

foo.addEventListener("message", function(event) {
  console.log(`message: ${event.data}`);
})
foo.addEventListener("close", function(event) {
  console.log(`close: ${event.data}`)
  foo.close()
})
foo.addEventListener("error", function(event) {
  console.log(`error: ${event.data}`)
})

foo.addEventListener("open", (event) => afterOpen());
if (foo.readyState === foo.OPEN) afterOpen();
