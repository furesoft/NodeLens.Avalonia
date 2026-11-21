<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e0000000-0000-4000-9000-000000000002" name="Units.Quantity">
  <nodes>
    <node id="e0000000-0000-4000-8000-000000000002" concept="Structure.Concept" name="Units.Quantity">
      <properties>
        <key>Units.Quantity</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>true</isRoot>
        <alias>quantity</alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>A number together with the unit it is measured in.</description>
        <nameSlot></nameSlot>
        <nameRule>camel</nameRule>
      </properties>
      <children role="properties">
        <node id="e0000000-0000-4000-8000-000000000102" concept="Structure.PropertyDecl" name="value">
          <properties>
            <name>value</name>
            <typeName>int</typeName>
            <defaultValue>0</defaultValue>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
        <node id="e0000000-0000-4000-8000-000000000103" concept="Structure.PropertyDecl" name="unit">
          <properties>
            <name>unit</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>