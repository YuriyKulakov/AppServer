/* eslint-disable react/prop-types */
import React from "react";
import { storiesOf } from "@storybook/react";
import { action } from '@storybook/addon-actions';
import { withKnobs, text, number, boolean, select } from "@storybook/addon-knobs/react";
import withReadme from "storybook-readme/with-readme";
import Readme from "./README.md";
import AdvancedSelector from "./";
import Section from "../../../.storybook/decorators/section";
import { BooleanValue } from "react-values";
import Button from "../button";
import { isEqual, slice } from "lodash";
import { name } from "faker";
//import ADSelectorMainBody from "./sub-components/sections/main/body";
import { FixedSizeList as List } from "react-window";
import InfiniteLoader from "react-window-infinite-loader";
import Loader from "../loader";
import { Text } from "../text";
import CustomScrollbarsVirtualList from "../scrollbar/custom-scrollbars-virtual-list";
import ADSelectorMainBody from "./sub-components/sections/main/body";

function getRandomInt(min, max) {
  return Math.floor(Math.random() * (max - min)) + min;
}

const groups = [
  {
    key: "group-all",
    label: "All groups",
    total: 0
  },
  {
    key: "group-dev",
    label: "Development",
    total: 0
  },
  {
    key: "group-management",
    label: "Management",
    total: 0
  },
  {
    key: "group-marketing",
    label: "Marketing",
    total: 0
  },
  {
    key: "group-mobile",
    label: "Mobile",
    total: 0
  },
  {
    key: "group-support",
    label: "Support",
    total: 0
  },
  {
    key: "group-web",
    label: "Web",
    total: 0
  },
  {
    key: "group-1",
    label: "Group1",
    total: 0
  },
  {
    key: "group-2",
    label: "Group2",
    total: 0
  },
  {
    key: "group-3",
    label: "Group3",
    total: 0
  },
  {
    key: "group-4",
    label: "Group4",
    total: 0
  },
  {
    key: "group-5",
    label: "Group5",
    total: 0
  }
];

const sizes = ["compact", "full"];
const displayTypes = ['dropdown', 'aside'];

class ADSelectorExample extends React.Component {
  constructor(props) {
    super(props);

    const { total, isOpen } = props;

    this.state = {
      isOpen: isOpen,
      options: [],
      hasNextPage: true,
      isNextPageLoading: false
    }

    this.AllOptions = Array.from({ length: total }, (v, index) => {
      const additional_group = groups[getRandomInt(1, 6)];
      groups[0].total++;
      additional_group.total++;
      return {
        key: `user${index}`,
        groups: ["group-all", additional_group.key],
        label: `User${index} ${name.findName()}`
      };
    });
  }

  loadNextPage = (startIndex, stopIndex) => {
    console.log(`loadNextPage(startIndex=${startIndex}, stopIndex=${stopIndex})`);
    this.setState({ isNextPageLoading: true }, () => {
      setTimeout(() => {
        const { options } = this.state;
        const newOptions = [...options].concat(slice(this.AllOptions, startIndex, startIndex+100))

        this.setState({
          hasNextPage: newOptions.length < this.props.total,
          isNextPageLoading: false,
          options: newOptions
        });

      }, 2500);
    });
  };

  componentDidUpdate(prevProps) {
    const {total, options, isOpen} = this.props;
    if(!isEqual(prevProps.options, options))
    {
      this.setState({
        options: options
      });
    }

    if(isOpen !== prevProps.isOpen) {
      this.setState({
        isOpen: isOpen
      });
    }

    if(total !== prevProps.total) {
      this.setState({
        total: total,
        option: []
      });
    }
  }

  render() {
    const { isOpen, options, hasNextPage, isNextPageLoading } = this.state;
    return (
      <AdvancedSelector
        options={options}
        hasNextPage={hasNextPage} 
        isNextPageLoading={isNextPageLoading}
        loadNextPage={this.loadNextPage}

        size={select("size", sizes, "full")}
        placeholder={text("placeholder", "Search users")}
        onSearchChanged={value => {
          action("onSearchChanged")(value);
          /*set(
            options.filter(option => {
              return option.label.indexOf(value) > -1;
            })
          );*/
        }}
        groups={groups}
        selectedGroups={[groups[0]]}
        isMultiSelect={boolean("isMultiSelect", true)}
        buttonLabel={text("buttonLabel", "Add members")}
        selectAllLabel={text("selectAllLabel", "Select all")}
        onSelect={selectedOptions => {
          action("onSelect")(selectedOptions);
          //toggle();
        }}
        //onCancel={toggle}
        onChangeGroup={group => {
          /*set(
            options.filter(option => {
              return (
                option.groups &&
                option.groups.length > 0 &&
                option.groups.indexOf(group.key) > -1
              );
            })
          );*/
        }}
        allowCreation={boolean("allowCreation", false)}
        onAddNewClick={() => action("onSelect") }
        isOpen={isOpen}
        displayType={select("displayType", displayTypes, "dropdown")}
      />
    );
  }

}

class MainListExample extends React.PureComponent {
  constructor(props) {
    super(props);

    this.AllOptions = Array.from({ length: props.total }, (v, index) => {
      const additional_group = groups[getRandomInt(1, 6)];
      groups[0].total++;
      additional_group.total++;
      return {
        key: `user${index}`,
        groups: ["group-all", additional_group.key],
        label: `User${index} ${name.findName()}`
      };
    });
  }

  state = {
    hasNextPage: true,
    isNextPageLoading: false,
    options: []
  };

  loadNextPage = (startIndex, stopIndex) => {
    console.log(`loadNextPage(startIndex=${startIndex}, stopIndex=${stopIndex})`);
    this.setState({ isNextPageLoading: true }, () => {
      setTimeout(() => {
        const { options } = this.state;
        const newOptions = [...options].concat(slice(this.AllOptions, startIndex, startIndex+100))

        this.setState({
          hasNextPage: newOptions.length < this.props.total,
          isNextPageLoading: false,
          options: newOptions
        });

      }, 2500);
    });
  };

  render() {
    const { hasNextPage, isNextPageLoading, options } = this.state;

    return (
      <ADSelectorMainBody 
        options={options}
        hasNextPage={hasNextPage} 
        isNextPageLoading={isNextPageLoading}
        loadNextPage={this.loadNextPage}
        listHeight={488}
        listWidth={356}
        itemHeight={32}
        onRowChecked={(option) => console.log("onRowChecked", option)}
        onRowSelect={(option) => console.log("onRowSelect", option)}
      />
    );
  }
}

storiesOf("Components|AdvancedSelector", module)
  .addDecorator(withKnobs)
  .addDecorator(withReadme(Readme))
  .add("base", () => {
    return (
      <Section>
        <BooleanValue
          defaultValue={true}
          onChange={() => action("isOpen changed")}
        >
          {({ value: isOpen, toggle }) => (
            <div style={{position: "relative"}}>
              <Button label="Toggle dropdown" onClick={toggle} />
              <ADSelectorExample isOpen={isOpen} total={number("Users count", 1000)} />
            </div>
          )}
        </BooleanValue>
      </Section>
    );
  })
  .add("list only", () => {
    return (
      <Section>
        <MainListExample total={number("Users count", 1000)} />
      </Section>
    );
  });
