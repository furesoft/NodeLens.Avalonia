<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9000-000000000003" name="Decl.Bool">
  <nodes>
    <node id="d0000000-0000-4000-8000-000000000003" concept="Structure.Concept" name="Decl.Bool">
      <properties>
        <key>Decl.Bool</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>A truth value literal.</description>
        <nameSlot></nameSlot>
        <nameRule>camel</nameRule>
      </properties>
      <children role="supertypes">
        <node id="d0000000-0000-4000-8000-000000000103" concept="Structure.SuperType" name="Decl.Expression">
          <properties>
            <conceptKey>Decl.Expression</conceptKey>
          </properties>
        </node>
      </children>
      <children role="properties">
        <node id="d0000000-0000-4000-8000-000000000203" concept="Structure.PropertyDecl" name="value">
          <properties>
            <name>value</name>
            <typeName>bool</typeName>
            <defaultValue>true</defaultValue>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>