<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9000-000000000002" name="Decl.Int">
  <nodes>
    <node id="d0000000-0000-4000-8000-000000000002" concept="Structure.Concept" name="Decl.Int">
      <properties>
        <key>Decl.Int</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>true</isRoot>
        <alias></alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>An integer literal.</description>
        <nameSlot></nameSlot>
        <nameRule>camel</nameRule>
      </properties>
      <children role="supertypes">
        <node id="d0000000-0000-4000-8000-000000000102" concept="Structure.SuperType" name="Decl.Expression">
          <properties>
            <conceptKey>Decl.Expression</conceptKey>
          </properties>
        </node>
      </children>
      <children role="properties">
        <node id="d0000000-0000-4000-8000-000000000202" concept="Structure.PropertyDecl" name="value">
          <properties>
            <name>value</name>
            <typeName>int</typeName>
            <defaultValue>0</defaultValue>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>