<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f0000000-0000-4000-9000-000000000005" name="Invoice.Bill">
  <nodes>
    <node id="f0000000-0000-4000-8000-000000000005" concept="Structure.Concept" name="Invoice.Bill">
      <properties>
        <key>Invoice.Bill</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>true</isRoot>
        <alias>bill</alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>A bill that holds the lines it is made of.</description>
        <nameSlot>name</nameSlot>
        <nameRule>pascal</nameRule>
      </properties>
      <children role="properties">
        <node id="f0000000-0000-4000-8000-000000000105" concept="Structure.PropertyDecl" name="name">
          <properties>
            <name>name</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="f0000000-0000-4000-8000-000000000205" concept="Structure.ChildDecl" name="lines">
          <properties>
            <name>lines</name>
            <childConceptKey>Invoice.Line</childConceptKey>
            <cardinality>0..n</cardinality>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>