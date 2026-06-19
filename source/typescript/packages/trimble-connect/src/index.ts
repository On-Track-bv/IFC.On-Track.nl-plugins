// Purpose: Trimble Connect bridge - maps Trimble Connect project data to BridgeData
// TODO: implement once Trimble Connect extension API is integrated

import type { BridgeData } from "@ifc-on-track/core";

export function loadBridgeData(): Promise<BridgeData> {
  throw new Error("Not yet implemented");
}

export function saveBridgeData(_data: BridgeData): Promise<void> {
  throw new Error("Not yet implemented");
}
