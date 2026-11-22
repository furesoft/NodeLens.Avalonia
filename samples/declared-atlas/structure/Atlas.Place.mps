<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b0000000-0000-4000-9000-000000000003" name="Atlas.Place">
  <nodes>
    <node id="b0000000-0000-4000-8000-000000000003" concept="Structure.Concept" name="Atlas.Place">
      <properties>
        <key>Atlas.Place</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>false</isRoot>
        <alias>place</alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>A spot with a name and a position.</description>
        <nameSlot>name</nameSlot>
      </properties>
      <children role="supertypes">
        <node id="b0000000-0000-4000-8000-000000000201" concept="Structure.SuperType" name="Atlas.Spot">
          <properties>
            <conceptKey>Atlas.Spot</conceptKey>
          </properties>
        </node>
      </children>
      <children role="properties">
        <node id="b0000000-0000-4000-8000-000000000202" concept="Structure.PropertyDecl" name="name">
          <properties>
            <name>name</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
        <node id="b0000000-0000-4000-8000-000000000203" concept="Structure.PropertyDecl" name="at">
          <properties>
            <name>at</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="b0000000-0000-4000-8000-000000000204" concept="Structure.ChildDecl" name="height">
          <properties>
            <name>height</name>
            <childConceptKey>Units.Quantity</childConceptKey>
            <cardinality>0..1</cardinality>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>