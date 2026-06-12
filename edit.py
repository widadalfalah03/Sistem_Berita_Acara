import json

filepath = "/etc/onlyoffice/documentserver/default.json"
with open(filepath, "r") as f:
    data = json.load(f)

data["services"]["CoAuthoring"]["request-filtering-agent"]["allowPrivateIPAddress"] = True
data["services"]["CoAuthoring"]["request-filtering-agent"]["allowMetaIPAddress"] = True

with open(filepath, "w") as f:
    json.dump(data, f, indent=2)

print("Updated default.json")
