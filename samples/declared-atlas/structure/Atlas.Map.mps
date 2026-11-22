<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b0000000-0000-4000-9000-000000000007" name="Atlas.Map">
  <nodes>
    <node id="b0000000-0000-4000-8000-000000000007" concept="Structure.Concept" name="Atlas.Map">
      <properties>
        <key>Atlas.Map</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>true</isRoot>
        <alias>map</alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>A named set of places and the routes between them.</description>
        <nameSlot>name</nameSlot>
      </properties>
      <children role="properties">
        <node id="b0000000-0000-4000-8000-000000000601" concept="Structure.PropertyDecl" name="name">
          <properties>
            <name>name</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="b0000000-0000-4000-8000-000000000602" concept="Structure.ChildDecl" name="places">
          <properties>
            <name>places</name>
            <childConceptKey>Atlas.Spot</childConceptKey>
            <cardinality>0..n</cardinality>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>