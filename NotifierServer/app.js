require("dotenv").config();
const fs = require("fs");
const os = require("os");
const path = require("path");
const https = require("https");
const express = require("express");
const uuidv1 = require("uuidv1");
const moment = require("moment");
const { Console } = require("console");
const WebSocketServer = require("ws").Server;

const color_blue = "\x1b[94m"; // bight
const color_red = "\x1b[35m"; // bight
const color_yellow = "\x1b[33m";
const color_green = "\x1b[32m"; // bright
const color_end = "\x1b[0m";

// console.log(
//   `Colors Test : ${color_blue}AAA${color_end} ${color_red}AAA${color_end} ${color_yellow}AAA${color_end} ${color_green}AAA${color_end}`
// );

// for (var x = 1; x < 100; x++) console.log(`\x1b[${x}mAAA\x1b[0m (${x})`);

const app = express();

console.log("OS: " + os.type());

// Certificate
var privateKey;
var certificate;
//const ca = fs.readFileSync('./ssl-pems/chain.pem', 'utf8');

if (/^Windows/i.test(os.type())) {
  privateKey = fs.readFileSync("./ssl-pems/privkey.pem", "utf8");
  certificate = fs.readFileSync("./ssl-pems/cert.pem", "utf8");
} else if (/^Linux/i.test(os.type())) {
  privateKey = fs.readFileSync(
    "/etc/privkey.pem",
    "utf8"
  );
  certificate = fs.readFileSync(
    "/etc/cert.pem",
    "utf8"
  );
}

const credentials = {
  key: privateKey,
  cert: certificate,
  // ca: ca
};

const httpsServer = https.createServer(credentials, app);

// app.get("/test-hook", function (req, res, next) {
//   console.log("got test-hook req: " + req.originalUrl);
//   res.end();
// });

// app.get("*", function (req, res, next) {
//   console.log("unhandled get: " + req.params[0]);
// });

// app.use((req, res) => {
//   res.send("Hello there !");
// });

const port = process.argv[2]; // 8082; // 443
httpsServer.listen(port, () => {
  console.log(
    `${color_blue}HTTPS Server running${color_end} on port: ${color_red}${port}${color_end}`
  );
});

let wss = new WebSocketServer({ server: httpsServer });
console.log(
  `${color_blue}WSS is listening${color_end} on port: ${color_red}${port}${color_end}`
);

wss.on("error", (error) => {
  console.log(`${color_blue}WebSocket.Server Error.${color_end}`);
});

var login_by_id = {};
var ws_by_login = {};
var to_whom_to_send_login_by_login = {}; // tracee to tracer
var last_info_message = {};

// const wws_port = 8083;
// let wss = new WebSocketServer({ port: wws_port, pingTimeout: 60000 });
// console.log("WWS is listening on port: " + wws_port);

wss.on("connection", (ws, req) => {
  var app_name = req.url;
  var app_ip = rAddr_to_ip(ws._socket.remoteAddress);

  function handlePingTimeout() {
    console.log(
      `${color_blue}Ping timeout. Closing the connection.${color_end} from: ${color_red}${
        login_by_id[ws.client_id]
      }${color_end}`
    );
    clearInterval(keepAliveInterval);
    ws.close(1001, "Ping timeout");
  }

  const keepAliveTimeout = 30000;
  let pingTimeout;

  const keepAliveInterval = setInterval(() => {
    if (ws.readyState == 1 /* lilo: WebSocket.OPEN */) {
      ws.ping();
    }
    pingTimeout = setTimeout(handlePingTimeout, keepAliveTimeout);
  }, keepAliveTimeout);

  ws.on("pong", () => {
    // console.log("Received Pong from client: " + login_by_id[ws.client_id]);
    clearTimeout(pingTimeout); // Reset the Ping timeout
  });

  ws.on("message", (message) => {
    var json = JSON.parse(message);

    switch (json.op) {
      case "client_login_name":
        var client_login_name = json.client_login_name;
        if (ws_by_login[client_login_name] != undefined) {
          ws.close(4001, "Login already in use");
          return;
        }
        ws_by_login[client_login_name] = ws;
        login_by_id[ws.client_id] = client_login_name;
        to_whom_to_send_login_by_login[client_login_name] =
          json.to_whom_to_send;

        var app_ip = rAddr_to_ip(ws._socket.remoteAddress);
        var app_name = req.url;
        console.log(
          `${color_blue}got WSS client_login_name${color_end} from: ${app_ip} ${app_name} client_login_name: ${color_red}${client_login_name}${color_end} connection_id: ${json.connection_id}`
        );

        // Notify My Tracer (Whom I Send the Info):
        var tracer_ws = ws_by_login[json.to_whom_to_send];
        if (tracer_ws != undefined) {
          var json = {
            op: "tracee_connected",
            tracee_login: client_login_name,
          };
          tracer_ws.send(JSON.stringify(json));
        }

        // Notify Me as Tracer: If I'm a destination of someone.
        var found_tracee = false;
        for (const key_tracee_login in to_whom_to_send_login_by_login) {
          var tracer_login = to_whom_to_send_login_by_login[key_tracee_login];
          if (tracer_login == client_login_name) {
            // found
            if (found_tracee) {
              throw "already found";
            }
            var tracer_ws = ws_by_login[key_tracee_login];
            if (tracer_ws != undefined) {
              var last_info_message_json = {
                op: "tracee_connected",
                tracee_login: key_tracee_login,
              };
              ws.send(JSON.stringify(last_info_message_json));
            }
            found_tracee = true;
          }
        }

        // Immediately Send Me Last Message:
        var _last_info_message = last_info_message[client_login_name];
        if (_last_info_message != undefined) {
          var last_info_message_json = JSON.parse(_last_info_message);
          last_info_message_json.Current_time = getCurrentTimeWithTimeZone(); // Update Current_time
          last_info_message_json.is_tracee_connected = found_tracee;
          ws.send(JSON.stringify(last_info_message_json));
          console.log(
            `${color_blue}also Sending back last_info_message:${color_end} ` +
              _last_info_message
          );
        }

        break;

      case "client_info":
        console.log(
          `${color_blue}Received client_info:${color_end} ` + message
        );

        // var _ws = ws_by_id[ws.client_id];
        var my_login = login_by_id[ws.client_id];
        var to_whom_to_send_login = to_whom_to_send_login_by_login[my_login];
        var tracer_ws = ws_by_login[to_whom_to_send_login];
        if (tracer_ws != undefined) {
          json.Current_time = getCurrentTimeWithTimeZone(); // new Date(); new Date().toISOString(); // lilo: override. No need to send?
          tracer_ws.send(JSON.stringify(json));
        }

        if (
          json.Last_keyboard_time != "0001-01-01T00:00:00" ||
          json.Last_mouse_time != "0001-01-01T00:00:00"
        ) {
          //   console.log(
          //     `${color_blue}last_info_message updated!!!${color_end} Last_keyboard_time: ${json.Last_keyboard_time} Last_mouse_time: ${json.Last_mouse_time}`
          //   );
          last_info_message[to_whom_to_send_login] = message;
        }

        break;

      case "show_custom_popup":
      case "custom_popup_response":
      case "close_custom_popup":
        console.log(`${color_blue}Received ${json.op}:${color_end} ` + message);
        var tracee_ws = ws_by_login[json.to_whom_to_send];
        if (tracee_ws != undefined) {
          json.Current_time = getCurrentTimeWithTimeZone(); // new Date(); new Date().toISOString(); // lilo: override. No need to send?
          tracee_ws.send(JSON.stringify(json));
        }
        break;

      default:
        throw "Unrecognized json.op: " + json.op;
    }
  });

  ws.on("close", function () {
    var login = login_by_id[ws.client_id];
    console.log(
      `${color_blue}WSS is Closed${color_end}: ${color_red}${login}${color_end}`
    );

    // Notify Disconnection to whom I Notify
    var tracer_login = to_whom_to_send_login_by_login[login];
    var tracer_ws = ws_by_login[tracer_login];
    if (tracer_ws != undefined) {
      var json = {
        op: "tracee_disconnected",
        // tracee_login: login,
      };
      tracer_ws.send(JSON.stringify(json));
    }

    // Delete connection
    delete ws_by_login[login];
    delete to_whom_to_send_login_by_login[login];
    delete login_by_id[ws.client_id];

    clearInterval(keepAliveInterval);
  });

  ws.on("error", (error) => {
    console.error(`WSS error: ${error.message}`);
  });

  var app_name = req.url;
  var app_ip = rAddr_to_ip(ws._socket.remoteAddress);
  var client_id = uuidv1();
  console.log(
    `${color_blue}got WSS connection from${color_end}: ${color_red}${app_ip}${color_end} ${app_name} client_id: ${client_id}`
  );
  ws.client_id = client_id;
  var ws_connected_json_data = {
    op: "ws_connected",
    client_id: client_id,
  };
  ws.send(JSON.stringify(ws_connected_json_data));
  // login_by_id[client_id] = login;
});

/*********/
/* UTILS */
/*********/

function rAddr_to_ip(ip_long) {
  var ip;
  var ip_res = /\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/g.exec(ip_long);
  if (ip_res != null) {
    ip = ip_res[0];
  } else {
    ip = "127.0.0.1";
  }
  return ip;
}

function DateTime_to_Hour(datetime_str) {
  const datetime = new Date(datetime_str);
  const hours = datetime.getUTCHours();
  const minutes = datetime.getUTCMinutes();
  const seconds = datetime.getUTCSeconds();
  return (timeString = `${hours}:${minutes}:${seconds}`);
}

function getCurrentTimeWithTimeZone() {
  let now = moment();
  let iso = now.format("YYYY-MM-DDTHH:mm:ss.SSSZ"); // Convert it to ISO 8601 format with timezone offset
  return iso;
}
