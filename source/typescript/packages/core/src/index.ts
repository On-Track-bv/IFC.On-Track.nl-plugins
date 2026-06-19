// Purpose: Bridge types matching contracts/bridge-data.schema.json
// Run `npm run generate-types` to regenerate from schema

export interface BridgeData {
  ifcData: IfcEntity[];
  settings?: BridgeSettings;
  propertyIsInstanceMap?: Record<string, boolean> | null;
}

export interface IfcEntity {
  type?: string | null;
  name?: string | null;
  description?: string | null;
  objectType?: string | null;
  tag?: string | null;
  predefinedType?: string | null;
  isDefinedBy?: IfcPropertySet[] | null;
  hasAssociations?: IfcClassificationReference[] | null;
}

export interface IfcPropertySet {
  type: string;
  name?: string | null;
  hasProperties?: IfcProperty[] | null;
}

export interface IfcProperty {
  type: string;
  name?: string | null;
  nominalValue?: unknown;
}

export interface IfcClassificationReference {
  type: string;
  location?: string | null;
  identification?: string | null;
  name?: string | null;
  referencedSource?: IfcClassification;
}

export interface IfcClassification {
  type: string;
  location?: string | null;
  name?: string | null;
}

export interface BridgeSettings {
  bsddApiEnvironment?: "production" | "test";
  mainDictionary?: BsddDictionary;
  ifcDictionary?: BsddDictionary;
  filterDictionaries?: BsddDictionary[] | null;
  language?: string;
  includeTestDictionaries?: boolean;
}

export interface BsddDictionary {
  ifcClassification?: IfcClassification;
  parameterMapping?: string | null;
}
